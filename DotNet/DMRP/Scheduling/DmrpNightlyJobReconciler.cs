using LantanaGroup.Link.DMRP.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>
    /// Keeps exactly one <see cref="DmrpNightlyJob"/> per facility timezone. Job existence follows
    /// the set of timezones in use, not the facility lifecycle: enrollment changes never touch
    /// Quartz, and a zone whose last facility leaves is swept at the next boot.
    /// </summary>
    public interface IDmrpNightlyJobReconciler
    {
        /// <summary>Idempotent. Creates the zone's job if missing; reschedules if the cron changed.</summary>
        Task EnsureZoneJobAsync(string timeZoneId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensure every zone facilities use; delete jobs for zones none use. A directory that names
        /// no zones at all leaves the existing jobs alone - see the implementation.
        /// </summary>
        Task ReconcileAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Delete every zone job. Run when DMRP is turned off.</summary>
        Task RemoveAllAsync(CancellationToken cancellationToken = default);
    }

    public sealed class DmrpNightlyJobReconciler : IDmrpNightlyJobReconciler
    {
        public const string JobGroup = "DmrpNightly";

        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<DmrpSettings> _settings;
        private readonly ILogger<DmrpNightlyJobReconciler> _logger;

        public DmrpNightlyJobReconciler(ISchedulerFactory schedulerFactory, IServiceScopeFactory scopeFactory,
            IOptionsMonitor<DmrpSettings> settings, ILogger<DmrpNightlyJobReconciler> logger)
        {
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task EnsureZoneJobAsync(string timeZoneId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // The facility API validates timezones, so this is a row that predates that check.
                _logger.LogError(ex, "No DMRP nightly job for timezone {TimeZone}: the platform does not know it.", timeZoneId);
                return;
            }

            var cron = _settings.CurrentValue.Scheduling.ResolvedNightlyCron;
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var jobKey = new JobKey(timeZoneId, JobGroup);

            if (!await scheduler.CheckExists(jobKey, cancellationToken))
            {
                var job = JobBuilder.Create<DmrpNightlyJob>()
                    .WithIdentity(jobKey)
                    .WithDescription($"DMRP nightly scheduling for {timeZoneId}")
                    .UsingJobData(DmrpNightlyJob.TimeZoneKey, timeZoneId)
                    .StoreDurably()
                    .Build();

                try
                {
                    await scheduler.ScheduleJob(job, BuildTrigger(jobKey, cron, timeZone), cancellationToken);

                    _logger.LogInformation("Scheduled DMRP nightly job for timezone {TimeZone} ({Cron}).", timeZoneId, cron);
                    return;
                }
                catch (ObjectAlreadyExistsException ex)
                {
                    // Another cluster node's ReconcileAllAsync won the race between our CheckExists
                    // and this ScheduleJob. Fall through to the verify/reschedule logic below instead
                    // of crashing the loser out of the hosted service's StartAsync.
                    _logger.LogDebug(ex, "DMRP nightly job for timezone {TimeZone} was scheduled by another node; verifying its trigger.", timeZoneId);
                }
            }

            var existing = (await scheduler.GetTriggersOfJob(jobKey, cancellationToken)).OfType<ICronTrigger>().FirstOrDefault();

            if (existing is null)
            {
                await scheduler.ScheduleJob(BuildTrigger(jobKey, cron, timeZone), cancellationToken);
                return;
            }

            if (existing.CronExpressionString != cron || !Equals(existing.TimeZone, timeZone))
            {
                await scheduler.RescheduleJob(existing.Key, BuildTrigger(jobKey, cron, timeZone), cancellationToken);

                _logger.LogInformation("Rescheduled DMRP nightly job for timezone {TimeZone}: {OldCron} -> {NewCron}.",
                    timeZoneId, existing.CronExpressionString, cron);
            }
        }

        public async Task ReconcileAllAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> wanted;
            using (var scope = _scopeFactory.CreateScope())
            {
                wanted = await scope.ServiceProvider.GetRequiredService<IFacilityDirectory>().GetTimeZonesAsync(cancellationToken);
            }

            foreach (var zone in wanted)
            {
                await EnsureZoneJobAsync(zone, cancellationToken);
            }

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var existing = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(JobGroup), cancellationToken);

            if (wanted.Count == 0 && existing.Count > 0)
            {
                // A directory read that came back empty for a transient reason would otherwise
                // unschedule the whole fleet, and nothing would put it back until someone saved a
                // facility. Zero zones next to jobs that exist is far more likely a failed read than
                // every facility being deleted at once, and the orphan sweep is only ever a tidy-up:
                // a zone job no facility uses produces nothing when it fires. Keep them and say so.
                _logger.LogWarning(
                    "The facility directory named no timezones while {Count} DMRP nightly job(s) exist; keeping them rather than unscheduling the fleet on what may be a failed read.",
                    existing.Count);

                return;
            }

            foreach (var orphan in existing.Where(k => !wanted.Contains(k.Name, StringComparer.Ordinal)))
            {
                await scheduler.DeleteJob(orphan, cancellationToken);
                _logger.LogInformation("Removed DMRP nightly job for timezone {TimeZone}: no facility uses it.", orphan.Name);
            }
        }

        public async Task RemoveAllAsync(CancellationToken cancellationToken = default)
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var keys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(JobGroup), cancellationToken);

            if (keys.Count > 0)
            {
                await scheduler.DeleteJobs(keys, cancellationToken);
                _logger.LogInformation("Removed {Count} DMRP nightly job(s); DMRP is disabled.", keys.Count);
            }
        }

        private static ITrigger BuildTrigger(JobKey jobKey, string cron, TimeZoneInfo timeZone) =>
            TriggerBuilder.Create()
                .ForJob(jobKey)
                .WithIdentity(jobKey.Name, JobGroup)
                .WithDescription(cron)
                .WithCronSchedule(cron, x => x
                    .InTimeZone(timeZone)
                    // A fire missed while the service was down runs once on recovery, anchored on
                    // its scheduled time, rather than being dropped or run once per missed night.
                    .WithMisfireHandlingInstructionFireAndProceed())
                .Build();
    }
}
