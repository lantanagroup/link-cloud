using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Services.Security;
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
                _logger.LogError(ex, "No DMRP nightly job for timezone {TimeZone}: the platform does not know it.", timeZoneId.SanitizeForLog());
                return;
            }

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            // Not recoverable: a night cut off by a dying instance is not re-run on another one.
            var definition = new CronJobDefinition
            {
                JobKey = new JobKey(timeZoneId, JobGroup),
                JobType = typeof(DmrpNightlyJob),
                Cron = _settings.CurrentValue.Scheduling.ResolvedNightlyCron,
                TimeZone = timeZone,
                Description = $"DMRP nightly scheduling for {timeZoneId}",
                JobData = new Dictionary<string, string> { [DmrpNightlyJob.TimeZoneKey] = timeZoneId }
            };

            var result = await CronJobReconciler.EnsureAsync(scheduler, definition, cancellationToken);

            switch (result.Outcome)
            {
                case CronJobReconcileOutcome.Created:
                    _logger.LogInformation("Scheduled DMRP nightly job for timezone {TimeZone} ({Cron}).",
                        timeZoneId.SanitizeForLog(), result.Cron.SanitizeForLog());
                    break;
                case CronJobReconcileOutcome.TriggerAdded:
                    _logger.LogInformation(
                        "DMRP nightly job for timezone {TimeZone} was stored without its trigger; added it ({Cron}).",
                        timeZoneId.SanitizeForLog(), result.Cron.SanitizeForLog());
                    break;
                case CronJobReconcileOutcome.Rescheduled:
                    _logger.LogInformation(
                        "Rescheduled DMRP nightly job for timezone {TimeZone}: {OldCron} -> {NewCron}.",
                        timeZoneId.SanitizeForLog(),
                        result.PreviousCron.SanitizeForLog(),
                        result.Cron.SanitizeForLog());
                    break;
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
                _logger.LogInformation("Removed DMRP nightly job for timezone {TimeZone}: no facility uses it.", orphan.Name.SanitizeForLog());
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
    }
}
