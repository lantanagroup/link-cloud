namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Defines a cohort of patients for measure-eligibility generation.
/// Each cohort specifies how many patients to generate, their per-measure
/// eligibility, and which clinical scenarios are allowed as the source pool.
/// <para>
/// Lives in the Automation library alongside <see cref="PatientProfile"/>,
/// <see cref="MeasureEligibility"/>, and <see cref="ClinicalScenarioEligibility"/>
/// so both Automation.UI and BackendE2ETests use identical cohort infrastructure.
/// </para>
/// </summary>
public class PatientCohortDefinition
{
    public int PatientCount { get; set; }

    /// <summary>
    /// Optional scheduled-report inpatient behavior for patients in this cohort.
    /// When set, scheduled-report automation uses this to decide admit/discharge timing.
    /// </summary>
    public ScheduledInpatientPattern? ScheduledInpatientPattern { get; set; }

    /// <summary>
    /// Per-measure eligibility map. Every selected measure should have an entry.
    /// Drives encounter type (inpatient vs ambulatory) and measure-specific
    /// resource generation (e.g., diabetic medication for Hypo).
    /// </summary>
    public Dictionary<ProfiledMeasureType, MeasureEligibility> MeasureEligibilities { get; set; } = new();

    public List<string> EligibleClinicalScenarioIds { get; set; } = [];
    public int ResourcesPerPatientMin { get; set; } = 50;
    public int ResourcesPerPatientMax { get; set; } = 100;

    /// <summary>
    /// Returns the eligibility for a specific measure.
    /// Measures not in the map are treated as non-qualifying.
    /// </summary>
    public MeasureEligibility GetEligibility(ProfiledMeasureType measure)
        => MeasureEligibilities.TryGetValue(measure, out var e) ? e : MeasureEligibility.NonQualifying;

    /// <summary>
    /// Returns true when this cohort qualifies for ALL of the specified measures.
    /// </summary>
    public bool QualifiesForAll(IReadOnlyList<ProfiledMeasureType> measures)
        => measures.All(m => GetEligibility(m) == MeasureEligibility.Qualifying);

    /// <summary>
    /// Returns true when this cohort qualifies for NONE of the specified measures.
    /// </summary>
    public bool QualifiesForNone(IReadOnlyList<ProfiledMeasureType> measures)
        => measures.All(m => GetEligibility(m) == MeasureEligibility.NonQualifying);

    /// <summary>
    /// Expands a list of cohorts into individual <see cref="PatientProfile"/> entries.
    /// This is the single shared expansion used by both Automation.UI and BackendE2ETests.
    /// </summary>
    public static List<PatientProfile> ExpandProfiles(IReadOnlyList<PatientCohortDefinition> cohorts, int seed)
    {
        var result = new List<PatientProfile>();
        var seedCursor = 0;

        foreach (var cohort in cohorts)
        {
            var count = Math.Max(0, cohort.PatientCount);
            var min = Math.Max(1, cohort.ResourcesPerPatientMin);
            var max = Math.Max(min, cohort.ResourcesPerPatientMax);
            var scenarios = cohort.EligibleClinicalScenarioIds is { Count: > 0 }
                ? cohort.EligibleClinicalScenarioIds
                : FhirGenerationCodes.ClinicalScenarios.Select(s => s.ScenarioId.ToString()).ToList();

            for (var i = 0; i < count; i++)
            {
                var seedOffset = seedCursor;
                var scenarioId = scenarios[seedCursor % scenarios.Count];
                var resources = min == max ? min : min + ((seed + seedCursor) % (max - min + 1));
                result.Add(new PatientProfile(
                    new Dictionary<ProfiledMeasureType, MeasureEligibility>(cohort.MeasureEligibilities),
                    seedOffset, scenarioId, resources, cohort.ScheduledInpatientPattern));
                seedCursor++;
            }
        }

        return result;
    }

    /// <summary>
    /// Convenience factory: creates a cohort where all specified measures are qualifying.
    /// </summary>
    public static PatientCohortDefinition AllQualifying(
        IReadOnlyList<ProfiledMeasureType> measures,
        int patientCount = 1,
        int resourcesMin = 50,
        int resourcesMax = 100)
    {
        return new PatientCohortDefinition
        {
            PatientCount = patientCount,
            MeasureEligibilities = measures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying),
            ResourcesPerPatientMin = resourcesMin,
            ResourcesPerPatientMax = resourcesMax
        };
    }

    /// <summary>
    /// Convenience factory: creates a cohort where all specified measures are non-qualifying.
    /// </summary>
    public static PatientCohortDefinition NoneQualifying(
        IReadOnlyList<ProfiledMeasureType> measures,
        int patientCount = 1,
        int resourcesMin = 50,
        int resourcesMax = 100)
    {
        return new PatientCohortDefinition
        {
            PatientCount = patientCount,
            MeasureEligibilities = measures.ToDictionary(m => m, _ => MeasureEligibility.NonQualifying),
            ResourcesPerPatientMin = resourcesMin,
            ResourcesPerPatientMax = resourcesMax
        };
    }
}
