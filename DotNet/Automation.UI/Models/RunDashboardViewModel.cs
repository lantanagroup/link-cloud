namespace Automation.UI.Models;

/// <summary>
/// ViewModel for the Runs dashboard page.
/// </summary>
public class RunDashboardViewModel
{
    public RunDashboardStats Stats { get; set; } = new();
    public IReadOnlyList<AutomationRunSummary> RecentRuns { get; set; } = [];
    public IReadOnlyList<AutomationRunSummary> ActiveRuns { get; set; } = [];
    public List<TestScenarioDefinition> SavedScenarios { get; set; } = [];
}
