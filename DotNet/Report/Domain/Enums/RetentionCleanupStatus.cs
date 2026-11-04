namespace LantanaGroup.Link.Report.Domain.Enums;

/// <summary>
/// The overall status of one retention cleanup, recalculated from its participant results on every reply.
/// Stored by Report and never sent on Kafka.
/// </summary>
public enum RetentionCleanupStatus
{
    /// <summary>
    /// At least one result is still pending, and none has failed.
    /// </summary>
    InProgress = 0,

    /// <summary>
    /// Every result is completed.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// At least one result has failed, and at least one has completed.
    /// </summary>
    Partial = 2,

    /// <summary>
    /// At least one result has failed, and none has completed.
    /// </summary>
    Failed = 3
}
