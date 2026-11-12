namespace Link.UI.Models;

public sealed class ReportBatchTrackingPage
{
    public bool Ready { get; init; }

    public bool Found { get; init; }

    public string? Message { get; init; }

    public ReportBatchTrackingModel? Batch { get; init; }
}

public sealed class ReportBatchTrackingModel
{
    public Guid BatchId { get; init; }

    public string EhrSystem { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string PillClass { get; init; } = string.Empty;

    public bool Open { get; init; }

    public bool CanCancel { get; init; }

    public int MaxConcurrency { get; init; }

    public string? CreatedAt { get; init; }

    public string? StartedAt { get; init; }

    public string? FinishedAt { get; init; }

    public int Total { get; init; }

    public int Pending { get; init; }

    public int InProgress { get; init; }

    public int Completed { get; init; }

    public int Failed { get; init; }

    public int Cancelled { get; init; }

    public IReadOnlyList<ReportBatchTrackingGroup> Groups { get; init; } = [];
}

public sealed class ReportBatchTrackingGroup
{
    public string EhrSystem { get; init; } = string.Empty;

    public IReadOnlyList<ReportBatchTrackingRow> Items { get; init; } = [];
}

public sealed class ReportBatchTrackingRow
{
    public int Ordinal { get; init; }

    public Guid ScenarioId { get; init; }

    public string ScenarioName { get; init; } = string.Empty;

    public bool IsMetricsRun { get; init; }

    public string EhrSystem { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string PillClass { get; init; } = string.Empty;

    public string? Error { get; init; }

    public Guid? RunId { get; init; }

    public string? RunHref { get; init; }
}

public sealed class ReportBatchHistoryPage
{
    public bool Ready { get; init; }

    public string? Message { get; init; }

    public IReadOnlyList<ReportBatchHistoryGroup> Groups { get; init; } = [];
}

public sealed class ReportBatchHistoryGroup
{
    public string EhrSystem { get; init; } = string.Empty;

    public IReadOnlyList<ReportBatchHistoryRow> Batches { get; init; } = [];
}

public sealed class ReportBatchHistoryRow
{
    public Guid BatchId { get; init; }

    public string Status { get; init; } = string.Empty;

    public string PillClass { get; init; } = string.Empty;

    public int ItemCount { get; init; }

    public int MaxConcurrency { get; init; }

    public string? CreatedAt { get; init; }

    public string? FinishedAt { get; init; }

    public string Href { get; init; } = string.Empty;
}
