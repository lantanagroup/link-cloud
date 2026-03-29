namespace Automation.UI.Models;

public class AutomationRunSummary
{
    public Guid RunId { get; set; }
    public AutomationScenarioKind Scenario { get; set; }
    public AutomationRunStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }
    public IReadOnlyList<string> Logs { get; set; } = [];
}
