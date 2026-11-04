namespace LantanaGroup.Link.Shared.Application.Extensions.Quartz;

/// <summary>
/// The result of <see cref="CronJobReconciler.EnsureAsync"/>, for the caller to log.
/// </summary>
public sealed record CronJobReconcileResult
{
    /// <summary>
    /// What changed in the store.
    /// </summary>
    public required CronJobReconcileOutcome Outcome { get; init; }

    /// <summary>
    /// The cron in the form Quartz stores it (uppercased).
    /// </summary>
    public required string Cron { get; init; }

    /// <summary>
    /// The cron the trigger had before it was rescheduled; null for any other outcome.
    /// </summary>
    public string? PreviousCron { get; init; }

    /// <summary>
    /// The trigger's next fire time, when Quartz reports one.
    /// </summary>
    public DateTimeOffset? NextFireTimeUtc { get; init; }
}
