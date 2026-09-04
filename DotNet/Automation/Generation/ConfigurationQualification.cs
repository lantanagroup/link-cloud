namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Derives per-measure Qualifying / Non-Qualifying from the clinical shape
/// (encounter class, status, antidiabetic insulin) plus census pattern.
/// This is a display and prediction artifact — not a generation switch.
/// </summary>
public static class ConfigurationQualification
{
    public static readonly ProfiledMeasureType[] KnownMeasures =
    [
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation,
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
    ];

    public static string ShortName(ProfiledMeasureType measure) => measure switch
    {
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => "ACH",
        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => "ACH Daily",
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => "Hypo",
        _ => measure.ToString()
    };

    public static QualificationPrediction Predict(
        PatientGenerationIntent? intent,
        FhirGenerationCodes.ClinicalScenarioDefinition? scenario,
        ScheduledInpatientPattern? pattern = null)
    {
        var encounterClass = ResolveEncounterClass(intent);
        var encounterStatus = string.IsNullOrWhiteSpace(intent?.EncounterStatus)
            ? "finished"
            : intent.EncounterStatus!;
        var includeInsulin = ResolveHypoglycemicInsulin(intent, scenario);
        var diabetesMed = EncounterIpClassification.IsDiabetesMedicationCode(intent?.MedicationAdministrationRxNorm);

        var statusOk = EncounterIpClassification.IsValidIpEncounterStatus(encounterStatus);
        var achIp = statusOk
            && EncounterIpClassification.ClassCodeQualifiesIp(encounterClass, EncounterIpClassification.IpProfile.Ach);
        var hypoIp = statusOk
            && EncounterIpClassification.ClassCodeQualifiesIp(encounterClass, EncounterIpClassification.IpProfile.Hypoglycemic);
        var hypoClinical = includeInsulin || diabetesMed;

        var elig = new Dictionary<ProfiledMeasureType, MeasureEligibility>();
        var reasons = new Dictionary<ProfiledMeasureType, string>();

        foreach (var measure in KnownMeasures)
        {
            var (qualifies, reason) = measure switch
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
                    or ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation
                    => achIp
                        ? (true, $"Encounter class {encounterClass} is an ACH initial-population class.")
                        : (false, $"Encounter class {encounterClass} is not an ACH initial-population class."),
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
                    => !hypoIp
                        ? (false, $"Encounter class {encounterClass} is not a Hypoglycemic inpatient class.")
                        : !hypoClinical
                            ? (false, "No antidiabetic (hypoglycemic insulin) medication on this configuration.")
                            : (true, "Inpatient class plus antidiabetic medication."),
                _ => (false, "Measure is not modeled.")
            };

            elig[measure] = qualifies ? MeasureEligibility.Qualifying : MeasureEligibility.NonQualifying;
            reasons[measure] = reason;
        }

        var census = (pattern ?? ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod)
            .GetCensusBehavior()
            .ExpectedInReport;

        return new QualificationPrediction(elig, reasons, census);
    }

    public static QualificationPrediction PredictFromConfiguration(
        PatientGenerationIntent? intent,
        string? clinicalScenarioId,
        ScheduledInpatientPattern? pattern = null)
        => Predict(intent, FhirGenerationCodes.GetScenarioById(clinicalScenarioId), pattern);

    public static void Stamp(PatientGenerationIntent? intent, string? clinicalScenarioId, out Dictionary<ProfiledMeasureType, MeasureEligibility> eligibilities, out MeasureEligibility cohortQualification)
    {
        var prediction = PredictFromConfiguration(intent, clinicalScenarioId);
        eligibilities = prediction.MeasureEligibilities;
        cohortQualification = prediction.CohortQualification;
    }

    public static string ResolveEncounterClass(PatientGenerationIntent? intent)
        => string.IsNullOrWhiteSpace(intent?.EncounterClass) ? "IMP" : intent.EncounterClass!;

    public static bool ResolveHypoglycemicInsulin(
        PatientGenerationIntent? intent,
        FhirGenerationCodes.ClinicalScenarioDefinition? scenario)
    {
        if (intent?.IncludeHypoglycemicInsulin is bool specified)
            return specified;
        return scenario != null
            && ClinicalScenarioEligibility.QualifiesForMeasure(
                scenario,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);
    }

    public static bool ScenarioImpliesHypoglycemicInsulin(FhirGenerationCodes.ClinicalScenarioDefinition scenario)
        => ClinicalScenarioEligibility.QualifiesForMeasure(
            scenario,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);
}

public sealed class QualificationPrediction(
    Dictionary<ProfiledMeasureType, MeasureEligibility> measureEligibilities,
    Dictionary<ProfiledMeasureType, string> reasons,
    bool censusPlacesInReport)
{
    public Dictionary<ProfiledMeasureType, MeasureEligibility> MeasureEligibilities { get; } = measureEligibilities;
    public IReadOnlyDictionary<ProfiledMeasureType, string> Reasons { get; } = reasons;
    public bool CensusPlacesInReport { get; } = censusPlacesInReport;

    public MeasureEligibility CohortQualification
        => MeasureEligibilities.Values.Any(v => v == MeasureEligibility.Qualifying)
            ? MeasureEligibility.Qualifying
            : MeasureEligibility.NonQualifying;

    public bool QualifiesFor(ProfiledMeasureType measure)
        => MeasureEligibilities.TryGetValue(measure, out var e) && e == MeasureEligibility.Qualifying;

    public bool ExpectedInReport(IReadOnlyList<ProfiledMeasureType> selectedMeasures)
        => CensusPlacesInReport
           && selectedMeasures.Count > 0
           && selectedMeasures.Any(QualifiesFor);
}
