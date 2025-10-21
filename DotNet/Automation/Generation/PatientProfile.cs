namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Per-patient profile that drives measure-aware generation.
/// Each profile carries a per-measure eligibility map so the generator knows
/// exactly which measures this patient should satisfy.
/// </summary>
/// <param name="MeasureEligibilities">
/// Per-measure eligibility map. Every selected measure must have an entry.
/// Drives encounter type (inpatient vs ambulatory) and measure-specific resources
/// (e.g., diabetic medication for Hypo).
/// </param>
/// <param name="SeedOffset">
/// Optional per-patient seed offset. When null the generator assigns one
/// automatically from the patient's ordinal position.
/// </param>
/// <param name="ClinicalScenarioId">
/// Optional stable clinical scenario ID override. When provided, generation uses this
/// scenario instead of deriving one from seed.
/// </param>
/// <param name="ResourcesPerPatient">
/// Optional per-patient resource target. When null, run-level default is used.
/// </param>
public record PatientProfile(
    Dictionary<ProfiledMeasureType, MeasureEligibility> MeasureEligibilities,
    int? SeedOffset = null,
    string? ClinicalScenarioId = null,
    int? ResourcesPerPatient = null,
    ScheduledInpatientPattern? ScheduledInpatientPattern = null,
    MeasureEligibility CohortQualification = MeasureEligibility.Qualifying)
{
    /// <summary>
    /// Returns true when this profile qualifies for the given measure.
    /// Measures not in the map are treated as non-qualifying.
    /// </summary>
    public bool QualifiesFor(ProfiledMeasureType measure)
        => MeasureEligibilities.TryGetValue(measure, out var e) && e == MeasureEligibility.Qualifying;

    /// <summary>
    /// Returns true when this profile requires an inpatient encounter
    /// (i.e., qualifies for any ACH-type measure).
    /// </summary>
    public bool RequiresInpatientEncounter()
        => QualifiesFor(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
           || QualifiesFor(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);

    /// <summary>
    /// Returns true when this profile qualifies for the Hypoglycemic measure,
    /// requiring diabetic medication generation.
    /// </summary>
    public bool RequiresHypoglycemicMedication()
        => QualifiesFor(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

    /// <summary>
    /// Returns true when this profile qualifies for ALL of the specified measures.
    /// </summary>
    public bool QualifiesForAll(IReadOnlyList<ProfiledMeasureType> measures)
        => measures.All(QualifiesFor);

    /// <summary>
    /// Returns true when this profile qualifies for at least one of the specified measures.
    /// A patient qualifying for any measure will appear in the report submission.
    /// </summary>
    public bool QualifiesForAny(IReadOnlyList<ProfiledMeasureType> measures)
        => measures.Any(QualifiesFor);

    /// <summary>
    /// Returns true when this profile qualifies for NONE of the specified measures.
    /// </summary>
    public bool QualifiesForNone(IReadOnlyList<ProfiledMeasureType> measures)
        => measures.All(m => !QualifiesFor(m));

    /// <summary>
    /// Returns true when this patient is expected to be report-eligible from cohort-level
    /// designation and inpatient-pattern inclusion rules.
    /// </summary>
    public bool IsExpectedInReportByCohortAndPattern()
    {
        if (CohortQualification == MeasureEligibility.NonQualifying)
            return false;

        var pattern = ScheduledInpatientPattern
            ?? global::LantanaGroup.Automation.Generation.ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod;
        return pattern.GetCensusBehavior().ExpectedInReport;
    }

    /// <summary>
    /// Returns true when this patient should be predicted as submitted for selected measures.
    /// </summary>
    public bool IsExpectedToBeSubmitted(IReadOnlyList<ProfiledMeasureType> measures)
        => IsExpectedInReportByCohortAndPattern() && QualifiesForAny(measures);

    /// <summary>
    /// Builds a map of pipeline measure ID → number of qualifying patients from
    /// concrete cohort/profile data. This allows validators to know the expected
    /// shape of pipeline output without interrogating the pipeline's own data.
    /// </summary>
    /// <param name="profiles">Ordered patient profiles (same order as <paramref name="patientIds"/>).</param>
    /// <param name="selectedMeasures">Ordered measure enums used during generation.</param>
    /// <param name="measureIds">
    /// Ordered pipeline measure ID strings (same order as <paramref name="selectedMeasures"/>),
    /// e.g. from <c>MeasureLoader.MeasureIds</c>.
    /// </param>
    public static Dictionary<string, int> BuildQualifyingCountPerMeasure(
        IReadOnlyList<PatientProfile> profiles,
        IReadOnlyList<ProfiledMeasureType> selectedMeasures,
        IReadOnlyList<string> measureIds)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < measureIds.Count && i < selectedMeasures.Count; i++)
        {
            var measureType = selectedMeasures[i];
            var count = profiles.Count(p => p.IsExpectedInReportByCohortAndPattern() && p.QualifiesFor(measureType));
            result[measureIds[i]] = count;
        }

        return result;
    }
}
