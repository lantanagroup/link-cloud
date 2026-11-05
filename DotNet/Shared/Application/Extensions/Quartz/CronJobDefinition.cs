using Quartz;

namespace LantanaGroup.Link.Shared.Application.Extensions.Quartz;

/// <summary>
/// A durable Quartz job with one cron trigger, as <see cref="CronJobReconciler"/> keeps it in the store.
/// </summary>
public sealed record CronJobDefinition
{
    /// <summary>
    /// The job's key. Its trigger uses the same name and group.
    /// </summary>
    public required JobKey JobKey { get; init; }

    /// <summary>
    /// The <see cref="IJob"/> implementation to run.
    /// </summary>
    public required Type JobType { get; init; }

    /// <summary>
    /// The cron expression. It must be a valid Quartz expression that can fire; see
    /// <see cref="CronJobReconciler.CanFire"/>.
    /// </summary>
    public required string Cron { get; init; }

    /// <summary>
    /// The time zone the cron is evaluated in.
    /// </summary>
    public required TimeZoneInfo TimeZone { get; init; }

    /// <summary>
    /// Whether another cluster instance re-runs a firing cut off by an instance that died.
    /// </summary>
    public bool RequestRecovery { get; init; }

    /// <summary>
    /// The job's description in the store.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Values stored in the job's data map.
    /// </summary>
    public IReadOnlyDictionary<string, string>? JobData { get; init; }
}
