using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace LantanaGroup.Link.Report.Domain.Models;

/// <summary>
/// The outcome of asking for a retention cleanup: whether one was recorded, which reports it covers, and
/// the request to publish, if any.
/// </summary>
public record class RetentionCleanupCreation
{
    /// <summary>
    /// The recorded cleanup's id. Null when no cleanup was recorded because none of the reports could be
    /// taken.
    /// </summary>
    public Guid? RetentionCleanupId { get; init; }

    /// <summary>
    /// <see cref="RetentionCleanupStatus.InProgress"/> for a cleanup to publish, or
    /// <see cref="RetentionCleanupStatus.Failed"/> when the participant list is invalid. Null when no
    /// cleanup was recorded.
    /// </summary>
    public RetentionCleanupStatus? Status { get; init; }

    /// <summary>
    /// Why the cleanup was recorded failed. Null otherwise.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// The reports the cleanup covers. They are now <c>PendingDeletion</c>, unless the cleanup failed, in which
    /// case they are still <c>None</c>.
    /// </summary>
    public IReadOnlyList<Guid> IncludedReportScheduleIds { get; init; } = [];

    /// <summary>
    /// The requested reports that were left out because they do not exist or are no longer <c>None</c>, for
    /// example because a cleanup that ran first took them.
    /// </summary>
    public IReadOnlyList<Guid> SkippedReportScheduleIds { get; init; } = [];

    /// <summary>
    /// The request to publish on <c>RetentionCheckRequested</c>. Null when nothing must be sent: no cleanup was
    /// recorded, or it was recorded failed.
    /// </summary>
    public RetentionCheckRequestedValue? Request { get; init; }
}
