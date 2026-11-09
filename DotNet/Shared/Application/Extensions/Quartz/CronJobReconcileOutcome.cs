namespace LantanaGroup.Link.Shared.Application.Extensions.Quartz;

/// <summary>
/// What <see cref="CronJobReconciler.EnsureAsync"/> changed in the store.
/// </summary>
public enum CronJobReconcileOutcome
{
    /// <summary>
    /// The job and its trigger were stored.
    /// </summary>
    Created,

    /// <summary>
    /// The stored job and trigger already matched, so nothing changed.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The trigger's cron or time zone differed and it was replaced.
    /// </summary>
    Rescheduled,

    /// <summary>
    /// The job was stored without its trigger, and the trigger was added.
    /// </summary>
    TriggerAdded
}
