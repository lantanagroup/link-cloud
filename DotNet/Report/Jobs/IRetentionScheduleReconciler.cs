namespace LantanaGroup.Link.Report.Jobs;

/// <summary>
/// Brings the retention job and its trigger in the clustered Quartz store in line with Retention:Schedule.
/// </summary>
public interface IRetentionScheduleReconciler
{
    /// <summary>
    /// Creates, reschedules or removes the retention job. Safe to run on every instance at every startup.
    /// </summary>
    Task ReconcileAsync(CancellationToken cancellationToken);
}
