using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Link.UI.Services.ReportBatches;

public static class ReportBatchStatus
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static bool IsOpen(string? status)
        => status is Queued or Running;
}

public static class ReportBatchItemStatus
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";

    public static bool IsTerminal(string? status)
        => status is Succeeded or Failed or Cancelled;
}

/// <summary>One batch of report runs for a single named EHR system.</summary>
[BsonIgnoreExtraElements]
public sealed class ReportBatchDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid BatchId { get; set; }

    public string EhrSystem { get; set; } = string.Empty;

    /// <summary>
    /// Set when this batch was started with other EHR batches from one launch.
    /// Absent on a batch started on its own.
    /// </summary>
    [BsonIgnoreIfNull]
    [BsonRepresentation(BsonType.String)]
    public Guid? LaunchId { get; set; }

    /// <summary>Cap copied when the batch was dispatched. A later settings change does not resize it.</summary>
    public int MaxConcurrency { get; set; }

    public string Status { get; set; } = ReportBatchStatus.Queued;

    public bool CancelRequested { get; set; }

    public List<ReportBatchItemDocument> Items { get; set; } = [];

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset CreatedAt { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? StartedAt { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? FinishedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ReportBatchItemDocument
{
    public int Ordinal { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ScenarioId { get; set; }

    public string ScenarioName { get; set; } = string.Empty;

    public bool IsMetricsRun { get; set; }

    [BsonIgnoreIfNull]
    [BsonRepresentation(BsonType.String)]
    public Guid? RunId { get; set; }

    public string Status { get; set; } = ReportBatchItemStatus.Queued;

    [BsonIgnoreIfNull]
    public string? Error { get; set; }
}

/// <summary>One EHR's scenarios and cap inside a multi-EHR launch.</summary>
public sealed record ReportBatchLaunchSlice(
    string? EhrSystem,
    IReadOnlyList<Guid>? ScenarioIds,
    int? MaxConcurrency);

/// <summary>The batches created for one launch. Each batch keeps its own EHR.</summary>
public sealed record ReportBatchLaunchStarted(
    Guid LaunchId,
    IReadOnlyList<ReportBatchDocument> Batches);
