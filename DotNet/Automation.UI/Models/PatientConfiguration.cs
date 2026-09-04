using LantanaGroup.Automation.Generation;

namespace Automation.UI.Models;

/// <summary>
/// Named, reusable patient generation shape. A scenario cohort references this
/// by id (live). Clone to freeze a copy.
/// </summary>
public sealed class PatientConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Derived from <see cref="Intent"/> (any measure predicted qualifying). Display cache.
    /// </summary>
    public MeasureEligibility CohortQualification { get; set; } = MeasureEligibility.Qualifying;

    /// <summary>
    /// Derived per-measure IP prediction from <see cref="Intent"/>. Display cache.
    /// </summary>
    public Dictionary<ProfiledMeasureType, MeasureEligibility> MeasureEligibilities { get; set; } = new();
    /// <summary>
    /// Admit/discharge placement relative to the scenario report period.
    /// Saved on the configuration so reuse does not depend on the cohort row.
    /// </summary>
    public ScheduledInpatientPattern? ScheduledInpatientPattern { get; set; }
    public List<string> ClinicalScenarioIds { get; set; } = [];
    public int ResourcesPerPatientMin { get; set; } = 50;
    public int ResourcesPerPatientMax { get; set; } = 100;

    public PatientGenerationIntent Intent { get; set; } = new();
}
