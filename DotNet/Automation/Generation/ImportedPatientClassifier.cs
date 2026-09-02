using Hl7.Fhir.Model;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Classifies an imported patient's FHIR resources into per-measure Q/NQ eligibilities
/// and (best-effort) the clinical scenario their data matches.
///
/// IP-membership predicates (encounter <c>class.code</c> + <c>status</c>) and the
/// approximate "Diabetes Medications" value set are delegated to
/// <see cref="EncounterIpClassification"/>, which is the single source of truth shared
/// with <see cref="MeasureInitialPopulationResolver"/>. This guarantees the classifier
/// and the resolver can never disagree about which encounter classes count as IP.
///
/// <para>
/// Heuristics (tracking the canonical NHSN CQL retrieves where feasible):
/// <list type="bullet">
///   <item>
///     <b>ACH-Monthly / ACH-Daily eligibility</b> &mdash; patient has an encounter with
///     a class code in <c>{IMP, ACUTE, NONAC, SS, EMER, OBSENC}</c> and a valid
///     IP status.
///   </item>
///   <item>
///     <b>Hypoglycemic eligibility</b> &mdash; patient has an encounter with a class
///     code in <c>{IMP, ACUTE, NONAC, SS}</c> and a valid IP status, AND either:
///     <list type="bullet">
///       <item>An antidiabetic <c>MedicationRequest</c> or <c>MedicationAdministration</c>
///             whose code matches <see cref="EncounterIpClassification.DiabetesMedicationCodes"/>
///             (closer to the CQL "Antidiabetic Drugs Administered or Ordered" predicate), OR</item>
///       <item>A diabetic <c>Condition</c> matching one of the diabetic clinical scenarios
///             (DKA / Diabetic Hypoglycemia) &mdash; retained as a permissive fallback for
///             sparse imported bundles that don't carry medication resources.</item>
///     </list>
///   </item>
///   <item>
///     <b>DetectedClinicalScenarioId</b> &mdash; best match against
///     <see cref="FhirGenerationCodes.ClinicalScenarios"/> by SNOMED or ICD primary
///     diagnosis code.
///   </item>
/// </list>
/// </para>
///
/// <para>
/// <b>Known modeling gaps</b> (mirrors those documented on
/// <see cref="EncounterIpClassification"/>): encounter-type-based IP qualification,
/// encounter-location-type-based IP qualification, and the full Diabetes Medications
/// value-set expansion (currently only RxNorm codes the generator emits are matched).
/// As a result, the classifier may under-detect Q/NQ for externally-authored bundles
/// that qualify via these unmodeled pathways &mdash; the user can always override the
/// pre-populated checkboxes in the scenario editor.
/// </para>
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
        var medicationRequests = new List<MedicationRequest>();
        var medicationAdministrations = new List<MedicationAdministration>();
        var medicationCodes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
        {
            switch (e?.Resource)
            {
                case Encounter enc: encounters.Add(enc); break;
                case Condition cond: conditions.Add(cond); break;
                case MedicationRequest mr: medicationRequests.Add(mr); break;
                case MedicationAdministration ma: medicationAdministrations.Add(ma); break;
                case Medication medication:
                    if (!string.IsNullOrWhiteSpace(medication.Id))
                        medicationCodes[medication.Id] = ExtractCodes(medication.Code);
                    break;
            }
        }

        var hasAchIpEncounter = encounters.Any(enc => QualifiesForIp(enc, EncounterIpClassification.IpProfile.Ach));
        var hasHypoIpEncounter = encounters.Any(enc => QualifiesForIp(enc, EncounterIpClassification.IpProfile.Hypoglycemic));

        // Heuristic for the CQL "Antidiabetic Drugs Administered or Ordered" predicate.
        // CQL additionally requires the drug to occur during the encounter's hospitalization
        // window; we currently only check existence — encounter linkage is a known gap.
        var hasAntidiabeticMedication =
            medicationRequests.Any(mr => IsAntidiabetic(mr.Medication, medicationCodes))
            || medicationAdministrations.Any(ma => IsAntidiabetic(ma.Medication, medicationCodes));

        // Permissive fallback for bundles that don't carry medications. Generator-produced
        // qualifying patients always include a diabetic Condition in the bundle, so this
        // keeps existing test fixtures green.
        var hasDiabeticCondition = conditions.Any(IsDiabeticHypoglycemicCondition);

        var detectedScenarioId = DetectScenario(conditions);

        var elig = new Dictionary<ProfiledMeasureType, MeasureEligibility>();
        foreach (var m in measures)
        {
            elig[m] = m switch
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
                    => hasAchIpEncounter ? MeasureEligibility.Qualifying : MeasureEligibility.NonQualifying,
                ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation
                    => hasAchIpEncounter ? MeasureEligibility.Qualifying : MeasureEligibility.NonQualifying,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
                    => (hasHypoIpEncounter && (hasAntidiabeticMedication || hasDiabeticCondition))
                        ? MeasureEligibility.Qualifying
                        : MeasureEligibility.NonQualifying,
                _ => MeasureEligibility.NonQualifying
            };
        }

        return new ClassificationResult(elig, detectedScenarioId);
    }

    private static bool QualifiesForIp(Encounter? enc, EncounterIpClassification.IpProfile profile)
    {
        if (enc == null) return false;
        if (!EncounterIpClassification.ClassCodeQualifiesIp(enc.Class?.Code, profile)) return false;
        if (!EncounterIpClassification.IsValidIpEncounterStatus(enc.Status?.ToString())) return false;
        return true;
    }

    private static bool IsAntidiabetic(
        DataType? medication,
        IReadOnlyDictionary<string, List<string>> medicationCodes)
    {
        if (HasAntidiabeticCoding((medication as CodeableConcept)?.Coding))
            return true;

        var reference = (medication as ResourceReference)?.Reference;
        if (string.IsNullOrWhiteSpace(reference))
            return false;

        var slash = reference.IndexOf('/');
        var id = slash >= 0 ? reference[(slash + 1)..] : reference;
        if (!medicationCodes.TryGetValue(id, out var codes))
            return false;
        return codes.Any(EncounterIpClassification.IsDiabetesMedicationCode);
    }

    private static bool HasAntidiabeticCoding(IEnumerable<Coding>? codings)
    {
        if (codings == null) return false;
        foreach (var coding in codings)
        {
            if (EncounterIpClassification.IsDiabetesMedicationCode(coding?.Code))
                return true;
        }
        return false;
    }

    private static List<string> ExtractCodes(CodeableConcept? concept)
    {
        if (concept?.Coding == null)
            return [];
        return concept.Coding
            .Select(c => c.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .ToList();
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
