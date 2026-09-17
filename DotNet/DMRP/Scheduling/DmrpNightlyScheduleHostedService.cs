using Microsoft.Extensions.Hosting;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>Reconciles the zone jobs once at boot. Registered only when DMRP is enabled.</summary>
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
