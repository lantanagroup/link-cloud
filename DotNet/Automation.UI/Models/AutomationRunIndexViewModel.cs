namespace Automation.UI.Models;

public class AutomationRunIndexViewModel
{
    public IReadOnlyList<AutomationRunSummary> Runs { get; set; } = [];
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long TotalCount { get; set; }

    /// <summary>Normalized sort token (camelCase). Defaults to "createdAt".</summary>
    public string SortBy { get; set; } = "createdAt";

    /// <summary>True for DESC, false for ASC. Default DESC = newest first.</summary>
    public bool SortDescending { get; set; } = true;

    public string SortDir => SortDescending ? "desc" : "asc";

    public int TotalPages => PageSize <= 0
        ? 1
        : (int)Math.Max(1, Math.Ceiling(TotalCount / (double)PageSize));
}
