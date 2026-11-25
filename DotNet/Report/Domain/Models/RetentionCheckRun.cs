namespace LantanaGroup.Link.Report.Domain.Models;

/// <summary>
/// Describes one firing of the retention job, as handed to the retention check.
/// </summary>
public record class RetentionCheckRun
{
    /// <summary>
    /// The UTC time the firing was due. On a cluster recovery this is when the interrupted run fired, to the second.
    /// </summary>
    /// <remarks>
    /// After an outage this is normally the earliest missed cron time (or the recovery instant on a Quartz store
    /// without the MISFIRE_ORIG_FIRE_TIME column), not when the check runs, so do not compute retention ages
    /// from it.
    /// See dev-docs/retention-schedule.md.
    /// </remarks>
    public DateTimeOffset ScheduledFireTimeUtc { get; init; }

    /// <summary>
    /// True when this run replaces one interrupted by a Report instance that died.
    /// </summary>
    public bool IsRecovery { get; init; }

    /// <summary>
    /// Quartz's id for this firing. A recovery run gets a new id.
    /// </summary>
    public string FireInstanceId { get; init; } = string.Empty;
}
