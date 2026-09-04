namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Per-patient profile that drives measure-aware generation.
/// Clinical shape lives on <see cref="Intent"/> (and the clinical scenario).
/// <see cref="MeasureEligibilities"/> is a derived IP prediction, not a generation switch.
/// </summary>
/// <param name="MeasureEligibilities">
/// Derived per-measure initial-population prediction from the clinical shape.
/// Used by report-membership prediction. Generation reads encounter class and
/// insulin from <see cref="Intent"/> / the clinical scenario instead.
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
    MeasureEligibility CohortQualification = MeasureEligibility.Qualifying,
    PatientGenerationIntent? Intent = null)
{
    /// <summary>
    /// Returns true when this profile qualifies for the given measure.
    /// Measures not in the map are treated as non-qualifying.
    /// </summary>
    public bool QualifiesFor(ProfiledMeasureType measure)
        => MeasureEligibilities.TryGetValue(measure, out var e) && e == MeasureEligibility.Qualifying;

    /// <summary>
    /// True when the clinical shape uses an ACH/Hypo inpatient encounter class.
    /// Story-pack default (no explicit class) is inpatient.
    /// </summary>
    public bool RequiresInpatientEncounter()
    {
        var classCode = Intent?.EncounterClass;
        if (string.IsNullOrWhiteSpace(classCode))
            return true;
        return EncounterIpClassification.ClassCodeQualifiesIp(classCode, EncounterIpClassification.IpProfile.Ach)
               || EncounterIpClassification.ClassCodeQualifiesIp(classCode, EncounterIpClassification.IpProfile.Hypoglycemic);
    }

    /// <summary>
    /// True when generation should include the hypoglycemic insulin pair.
    /// Driven by the configuration (explicit insulin flag or diabetic clinical profile).
    /// </summary>
    public bool RequiresHypoglycemicMedication()
    {
        var scenario = FhirGenerationCodes.GetScenarioById(ClinicalScenarioId);
        if (EncounterIpClassification.IsDiabetesMedicationCode(Intent?.MedicationAdministrationRxNorm))
            return true;
        return ConfigurationQualification.ResolveHypoglycemicInsulin(Intent, scenario);
    }

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
    /// True when the inpatient pattern places this stay in the report window.
    /// Measure IP is <see cref="QualifiesFor"/>; both must hold to predict submission.
    /// </summary>
    public bool IsExpectedInReportByCohortAndPattern()
    {
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
