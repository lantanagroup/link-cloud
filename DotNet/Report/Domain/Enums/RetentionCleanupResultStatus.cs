namespace LantanaGroup.Link.Report.Domain.Enums;

/// <summary>
/// Where one participant's result stands in a retention cleanup. Stored by Report and never sent on Kafka;
/// a completion carries only the wire values <c>Completed</c> and <c>Failed</c>.
/// </summary>
public enum RetentionCleanupResultStatus
{
    /// <summary>
    /// The participant has not replied for this result yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The participant finished, whether or not anything was left to delete.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// The participant could not finish, or a report was missing from its completion.
    /// </summary>
    Failed = 2
}
