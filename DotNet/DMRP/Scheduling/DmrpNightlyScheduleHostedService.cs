using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Impl.Matchers;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>
    /// Owns the shared Quartz scheduler while DMRP is enabled: it sweeps the host's classic report
    /// jobs, reconciles the zone jobs, and starts the scheduler - and shuts it down on stop.
    /// Registered only when DMRP is enabled.
    /// </summary>
    /// <remarks>
    /// With the flag on this service is the sole owner of the scheduler's start and shutdown; the
    /// host's classic <c>ScheduleService</c> is not hosted at all in that mode, so nothing else
    /// starts or stops it. The order inside <see cref="StartAsync"/> is the point: the classic jobs
    /// are deleted, and the zone jobs reconciled, before the scheduler is started, so a classic job
    /// left from before the flag was turned on cannot fire against a stale, snapshotted measure list
    /// in the window before it is deleted.
    /// </remarks>
    public sealed class DmrpNightlyScheduleHostedService : IHostedService
    {
        private readonly IDmrpNightlyJobReconciler _reconciler;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly DmrpSchedulingHostOptions _hostOptions;
        private readonly ILogger<DmrpNightlyScheduleHostedService> _logger;

        private IScheduler? _scheduler;

        public DmrpNightlyScheduleHostedService(IDmrpNightlyJobReconciler reconciler,
            ISchedulerFactory schedulerFactory, DmrpSchedulingHostOptions hostOptions,
            ILogger<DmrpNightlyScheduleHostedService> logger)
        {
            _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
            _hostOptions = hostOptions ?? throw new ArgumentNullException(nameof(hostOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            // Scheduling is the DMRP nightly job's while the flag is on. Classic per-facility jobs
            // left from before the flag would announce stale, snapshotted measure lists.
            var classic = await _scheduler.GetJobKeys(
                GroupMatcher<JobKey>.GroupEquals(_hostOptions.ClassicJobGroup), cancellationToken);

            if (classic.Count > 0)
            {
                await _scheduler.DeleteJobs(classic, cancellationToken);
                _logger.LogInformation("DMRP is enabled; removed {Count} classic report job(s).", classic.Count);
            }

            await _reconciler.ReconcileAllAsync(cancellationToken);

            // Last, and only here: everything the scheduler would fire is settled by now.
            await _scheduler.Start(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) =>
            _scheduler?.Shutdown(cancellationToken) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Removes the zone jobs at boot when DMRP is disabled, so flipping the flag off does not leave
    /// a scheduler firing a job whose services are no longer registered. Registered only when DMRP
    /// is disabled, above the module's early return.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="DmrpNightlyScheduleHostedService"/> this one deliberately does not start or
    /// stop the shared Quartz scheduler: with the flag off the host's own <c>ScheduleService</c> is
    /// hosted and owns that lifecycle. Registered ahead of it, so the zone jobs are gone by the time
    /// it starts the scheduler.
    /// </remarks>
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
