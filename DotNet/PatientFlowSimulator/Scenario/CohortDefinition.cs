namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// A group of patients that share a clinical template. Counts are per month.
/// </summary>
public sealed class CohortDefinition
{
    /// <summary>
    /// Name of the cohort, unique within its facility. Part of every patient's seed.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Name of the <see cref="ClinicalTemplate"/> the patients are generated from.
    /// </summary>
    public string Template { get; set; } = "";

    /// <summary>
    /// Patients generated for this cohort every month.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// How many of <see cref="Count"/> are admitted just before the month ends and discharged just after
    /// the next month starts.
    /// </summary>
    public int CrossMonthCount { get; set; }

    /// <summary>
    /// The measures these patients are built to qualify for. Drives the expected counts in the manifest.
    /// </summary>
    public List<string> QualifiesFor { get; set; } = [];

    /// <summary>
    /// Throws when the cohort cannot be generated as written.
    /// </summary>
    public void Validate(string facilityId, ScenarioDefinition scenario)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidDataException($"A cohort in facility '{facilityId}' has no name.");
        }

        if (Count < 0 || CrossMonthCount < 0 || CrossMonthCount > Count)
        {
            throw new InvalidDataException(
                $"Cohort '{Name}' in facility '{facilityId}' needs 0 <= crossMonthCount <= count.");
        }

        scenario.GetTemplate(Template);
    }
}
