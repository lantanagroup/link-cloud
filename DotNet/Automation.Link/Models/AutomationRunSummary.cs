namespace LantanaGroup.Link.Automation.Link.Models;

public class AutomationRunSummary
{
    public Guid RunId { get; set; }
    public string RunName { get; set; } = string.Empty;
    public AutomationScenarioKind Scenario { get; set; }
    public string SelectedMeasure { get; set; } = string.Empty;
    public int PatientCount { get; set; }
    public int ResourcesPerPatient { get; set; }
    public int Seed { get; set; }
    /// <summary>
    /// When true, this Automation run minted X-Metrics-Mode=performance at the report origin.
    /// </summary>
    public bool IsMetricsRun { get; set; }
    public AutomationRunStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }
    /// <summary>Human-readable pipeline duration (report created ? submitted).</summary>
    public string? Duration { get; set; }
    public string? FacilityId { get; set; }
    public string? ReportId { get; set; }
    public string? RunConfigurationJson { get; set; }
    public Guid? GeneratedTemplateCacheVersionId { get; set; }
    public int? GeneratedTemplateCacheVersionNumber { get; set; }
    public string? GeneratedTemplateCacheScenarioKey { get; set; }
    public string? GeneratedTemplateSetHash { get; set; }
    public IReadOnlyList<string> Logs { get; set; } = [];
}
