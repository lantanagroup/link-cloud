using System.Globalization;
using Link.UI.Models;
using Link.UI.Services;

namespace Link.UI.Services.ReportBatches;

/// <summary>
/// Words and grouping for the batch tracking page. Stored statuses stay as they are.
/// </summary>
public static class ReportBatchDisplay
{
    public const int HistoryLimit = 50;

    public const string Pending = "Pending";
    public const string InProgress = "In Progress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Unknown = "Unknown";

    public const string UnavailableMessage =
        "Report batches are unavailable until the automation engine is ready.";

    public static string MissingMessage(Guid batchId) => $"Batch {batchId} was not found.";

    public static string ItemLabel(string? status) => status switch
    {
        ReportBatchItemStatus.Queued => Pending,
        ReportBatchItemStatus.Running => InProgress,
        ReportBatchItemStatus.Succeeded => Completed,
        ReportBatchItemStatus.Failed => Failed,
        ReportBatchItemStatus.Cancelled => Cancelled,
        _ => string.IsNullOrWhiteSpace(status) ? Unknown : status.Trim()
    };

    public static string BatchLabel(string? status) => status switch
    {
        ReportBatchStatus.Queued => Pending,
        ReportBatchStatus.Running => InProgress,
        ReportBatchStatus.Completed => Completed,
        ReportBatchStatus.Cancelled => Cancelled,
        _ => string.IsNullOrWhiteSpace(status) ? Unknown : status.Trim()
    };

    public static string PillClass(string? label) => StatusPills.ForRun(label);

    public static string EhrName(string? name)
        => string.IsNullOrWhiteSpace(name) ? "Unnamed EHR" : name.Trim();

    public static string RunHref(Guid runId) => "/Automation/Runs/" + runId.ToString("D");

    public static string BatchHref(Guid batchId) => "/Automation/ReportBatches/" + batchId.ToString("D");

    public static ReportBatchTrackingModel From(ReportBatchDocument batch)
    {
        var ehr = EhrName(batch.EhrSystem);
        var rows = batch.Items
            .OrderBy(item => item.Ordinal)
            .Select(item => Row(item, ehr))
            .ToList();
        var label = BatchLabel(batch.Status);
        var open = ReportBatchStatus.IsOpen(batch.Status);

        return new ReportBatchTrackingModel
        {
            BatchId = batch.BatchId,
            EhrSystem = ehr,
            Status = label,
            PillClass = PillClass(label),
            Open = open,
            CanCancel = open,
            MaxConcurrency = batch.MaxConcurrency,
            CreatedAt = Instant(batch.CreatedAt),
            StartedAt = Instant(batch.StartedAt),
            FinishedAt = Instant(batch.FinishedAt),
            Total = rows.Count,
            Pending = rows.Count(row => row.Status == Pending),
            InProgress = rows.Count(row => row.Status == InProgress),
            Completed = rows.Count(row => row.Status == Completed),
            Failed = rows.Count(row => row.Status == Failed),
            Cancelled = rows.Count(row => row.Status == Cancelled),
            Groups =
            [
                new ReportBatchTrackingGroup
                {
                    EhrSystem = ehr,
                    Items = rows
                }
            ]
        };
    }

    public static IReadOnlyList<ReportBatchHistoryGroup> History(IEnumerable<ReportBatchDocument> batches)
        => batches
            .GroupBy(batch => EhrName(batch.EhrSystem), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReportBatchHistoryGroup
            {
                EhrSystem = group.Key,
                Batches = group.Select(HistoryRow).ToList()
            })
            .ToList();

    private static ReportBatchTrackingRow Row(ReportBatchItemDocument item, string ehr)
    {
        var label = ItemLabel(item.Status);
        return new ReportBatchTrackingRow
        {
            Ordinal = item.Ordinal,
            ScenarioId = item.ScenarioId,
            ScenarioName = string.IsNullOrWhiteSpace(item.ScenarioName) ? "Scenario" : item.ScenarioName.Trim(),
            IsMetricsRun = item.IsMetricsRun,
            EhrSystem = ehr,
            Status = label,
            PillClass = PillClass(label),
            Error = string.IsNullOrWhiteSpace(item.Error) ? null : item.Error.Trim(),
            RunId = item.RunId,
            RunHref = item.RunId is Guid runId ? RunHref(runId) : null
        };
    }

    private static ReportBatchHistoryRow HistoryRow(ReportBatchDocument batch)
    {
        var label = BatchLabel(batch.Status);
        return new ReportBatchHistoryRow
        {
            BatchId = batch.BatchId,
            Status = label,
            PillClass = PillClass(label),
            ItemCount = batch.Items.Count,
            MaxConcurrency = batch.MaxConcurrency,
            CreatedAt = Instant(batch.CreatedAt),
            FinishedAt = Instant(batch.FinishedAt),
            Href = BatchHref(batch.BatchId)
        };
    }

    private static string? Instant(DateTimeOffset? value)
        => value?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
