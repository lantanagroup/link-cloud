namespace Link.UI.Models;

public sealed class ReportBatchSettingsPage
{
    public int MaxConcurrency { get; init; }

    public int UpperBound { get; init; }

    public bool Ready { get; init; }

    public string? Message { get; init; }

    public string? Error { get; init; }
}
