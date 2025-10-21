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

    // Paging + sort state for the Recent Runs table. Echoed back from the
    // controller so the view can render headers, pager controls, and toggle
    // links without re-deriving anything client-side.
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
    public string SortBy { get; set; } = "createdAt";
    public bool SortDescending { get; set; } = true;
    public string SortDir => SortDescending ? "desc" : "asc";
}
