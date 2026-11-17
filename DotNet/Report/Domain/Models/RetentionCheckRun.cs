namespace LantanaGroup.Link.Report.Domain.Models;

/// <summary>
/// Describes one firing of the retention job, as handed to the retention check.
/// </summary>
public record class RetentionCheckRun
{
    /// <summary>
    /// The UTC time the firing was due. On a cluster recovery this is the original firing's time.
    /// </summary>
    /// <remarks>
    /// A misfire recovered after an outage reports the recovery instant, not the missed time, so do not
    /// compute retention ages from this value. See dev-docs/retention-schedule.md.
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
