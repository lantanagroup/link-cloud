using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace LantanaGroup.Link.Report.Domain.Models;

/// <summary>
/// Everything the caller computes before asking for a retention cleanup: the reports, their windows and
/// cutoffs, who started it, and how much data goes.
/// </summary>
public record class RetentionCleanupRequest
{
    /// <summary>
    /// The run that asked for the cleanup. One large run can split into several cleanups that share it.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// What started the cleanup.
    /// </summary>
    public required RetentionInitiatedBy InitiatedBy { get; init; }

    /// <summary>
    /// The user id of whoever started the cleanup: <c>SystemUser</c> for a scheduled run.
    /// </summary>
    public required string InitiatedByUserId { get; init; }

    /// <summary>
    /// The display name of whoever started the cleanup, as it is now.
    /// </summary>
    public required string InitiatedByUserName { get; init; }

    /// <summary>
    /// How much of each report's data goes. <see cref="RetentionMode.Purge"/> is allowed only on a
    /// single-report cleanup.
    /// </summary>
    public required RetentionMode Mode { get; init; }

    /// <summary>
    /// The cutoff for the system default window, in UTC, used by cutoff-scoped participants.
    /// </summary>
    public required DateTimeOffset CutoffDateTime { get; init; }

    /// <summary>
    /// The reports to clean up. Never empty, and each report at most once.
    /// </summary>
    public required IReadOnlyList<RetentionCleanupRequestReport> Reports { get; init; }
}
