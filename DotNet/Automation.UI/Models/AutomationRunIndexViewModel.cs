namespace Automation.UI.Models;

public class AutomationRunIndexViewModel
{
    public IReadOnlyList<AutomationRunSummary> Runs { get; set; } = [];
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long TotalCount { get; set; }

    public int TotalPages => PageSize <= 0
        ? 1
        : (int)Math.Max(1, Math.Ceiling(TotalCount / (double)PageSize));
}
