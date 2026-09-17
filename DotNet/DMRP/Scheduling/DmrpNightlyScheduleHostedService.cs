using Microsoft.Extensions.Hosting;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>Reconciles the zone jobs once at boot. Registered only when DMRP is enabled.</summary>
    /// <remarks>
    /// This service deliberately does not start or stop the shared Quartz scheduler:
    /// <c>ScheduleService</c> is the only place either happens. It has to be, because it is what
    /// deletes the classic per-facility jobs left from before the flag was turned on, and starting
    /// the scheduler here - ahead of it in the hosted-service order - would let those jobs fire
    /// against snapshotted measure lists in the window before they are deleted. This service runs
    /// first so the zone jobs exist by the time <c>ScheduleService</c> starts the scheduler.
    /// Removing that <c>Start</c>/<c>Shutdown</c> pair from <c>ScheduleService</c> means moving it
    /// here, after the classic-job cleanup moves here too; nothing else starts the scheduler.
    /// </remarks>
    public sealed class DmrpNightlyScheduleHostedService : IHostedService
    {
        private readonly IDmrpNightlyJobReconciler _reconciler;

        public DmrpNightlyScheduleHostedService(IDmrpNightlyJobReconciler reconciler)
        {
            _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
        }

        public Task StartAsync(CancellationToken cancellationToken) => _reconciler.ReconcileAllAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// Removes the zone jobs at boot when DMRP is disabled, so flipping the flag off does not leave
    /// a scheduler firing a job whose services are no longer registered. Registered only when DMRP
    /// is disabled, above the module's early return.
    /// </summary>
    public sealed class DmrpNightlyScheduleCleanupService : IHostedService
    {
        private readonly IDmrpNightlyJobReconciler _reconciler;

        public DmrpNightlyScheduleCleanupService(IDmrpNightlyJobReconciler reconciler)
        {
            _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
        }

        public Task StartAsync(CancellationToken cancellationToken) => _reconciler.RemoveAllAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
