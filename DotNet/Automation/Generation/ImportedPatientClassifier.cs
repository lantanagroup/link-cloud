using Hl7.Fhir.Model;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Classifies an imported patient's FHIR resources into per-measure Q/NQ eligibilities
/// and (best-effort) the clinical scenario their data matches.
///
/// The classifier intentionally uses the same heuristics as the generator:
///   * ACH-Monthly / ACH-Daily eligibility   = patient has an inpatient encounter (class IMP).
///   * Hypoglycemic eligibility              = patient qualifies for ACH AND has a Condition
///                                              whose code matches one of the diabetic
///                                              clinical scenarios (DKA / Diabetic Hypoglycemia).
///   * DetectedClinicalScenarioId            = best match against
///                                              <see cref="FhirGenerationCodes.ClinicalScenarios"/>
///                                              by SNOMED or ICD primary diagnosis code.
///
/// Results are advisory only and are intended to be presented to the user as
/// pre-populated checkboxes that they can override before saving the scenario.
/// </summary>
public static class ImportedPatientClassifier
{
    public sealed record ClassificationResult(
        Dictionary<ProfiledMeasureType, MeasureEligibility> MeasureEligibilities,
        string? DetectedClinicalScenarioId);

    /// <summary>
    /// Classify a patient given the FHIR entries that belong to them
    /// (typically: Patient + Encounter + Conditions + Observations + ...).
    /// </summary>
    public static ClassificationResult Classify(
        IEnumerable<Bundle.EntryComponent> entries,
        IReadOnlyList<ProfiledMeasureType> measures)
    {
        var encounters = new List<Encounter>();
        var conditions = new List<Condition>();

        foreach (var e in entries)
        {
            switch (e?.Resource)
            {
                case Encounter enc: encounters.Add(enc); break;
                case Condition cond: conditions.Add(cond); break;
            }
        }

        var hasInpatientEncounter = encounters.Any(IsInpatient);
        var hasDiabeticCondition = conditions.Any(IsDiabeticHypoglycemicCondition);

        var detectedScenarioId = DetectScenario(conditions);

        var elig = new Dictionary<ProfiledMeasureType, MeasureEligibility>();
        foreach (var m in measures)
        {
            elig[m] = m switch
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
                    => hasInpatientEncounter ? MeasureEligibility.Qualifying : MeasureEligibility.NonQualifying,
                ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation
                    => hasInpatientEncounter ? MeasureEligibility.Qualifying : MeasureEligibility.NonQualifying,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
                    => (hasInpatientEncounter && hasDiabeticCondition) ? MeasureEligibility.Qualifying : MeasureEligibility.NonQualifying,
                _ => MeasureEligibility.NonQualifying
            };
        }

        return new ClassificationResult(elig, detectedScenarioId);
    }

    private static bool IsInpatient(Encounter enc)
    {
        var code = enc?.Class?.Code;
        if (string.IsNullOrWhiteSpace(code)) return false;
        return string.Equals(code, "IMP", StringComparison.OrdinalIgnoreCase)
               || string.Equals(code, "ACUTE", StringComparison.OrdinalIgnoreCase)
               || string.Equals(code, "NONAC", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDiabeticHypoglycemicCondition(Condition cond)
    {
        var dkaIcd = "E11.10";
        var dkaSnomed = "420422005";
        var hypoIcd = "E11.649";
        var hypoSnomed = "421725003";

        foreach (var coding in cond?.Code?.Coding ?? [])
        {
            var c = coding?.Code;
            if (string.IsNullOrWhiteSpace(c)) continue;
            if (string.Equals(c, dkaIcd, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c, dkaSnomed, StringComparison.Ordinal)
                || string.Equals(c, hypoIcd, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c, hypoSnomed, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string? DetectScenario(IReadOnlyList<Condition> conditions)
    {
        foreach (var cond in conditions)
        {
            foreach (var coding in cond?.Code?.Coding ?? [])
            {
                var c = coding?.Code;
                if (string.IsNullOrWhiteSpace(c)) continue;

                foreach (var sc in FhirGenerationCodes.ClinicalScenarios)
                {
                    if (string.Equals(c, sc.PrimaryDxIcd, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(c, sc.PrimaryDxSnomed, StringComparison.Ordinal))
                    {
                        return sc.ScenarioId.ToString();
                    }
                }
            }
        }
        return null;
    }
}
