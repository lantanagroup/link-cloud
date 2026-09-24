using System.Linq.Expressions;
using Confluent.Kafka;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>
    /// One fire per timezone per night. Reads the zone's facilities live, refreshes next month's
    /// plans from DMRP on the last night of a month, and produces a ReportScheduled event for every
    /// period that starts at the coming midnight and has at least one dQM.
    /// </summary>
    /// <remarks>
    /// The JobDataMap carries only the timezone. Everything else is read at fire time, so there is
    /// no snapshot to keep in step with the facility table. All period math derives from the
    /// scheduled fire time rather than the wall clock, read once at the top of the fire so every
    /// facility in a zone is announced against the same night. That time is not enough on its own to
    /// recover a missed night: the trigger's misfire policy is FireOnceNow, which Quartz implements
    /// by moving the trigger's next fire time to the recovery instant, so a fire recovered after
    /// midnight carries the recovery time as its scheduled time. The configured nightly cron's
    /// nominal local time is passed alongside it, and a fire earlier in the local day than that is
    /// read as the previous night's - see <see cref="ReportingPeriods.ComingMidnight"/>.
    /// Facilities are independent: one failing is counted and logged, and the loop goes on.
    /// A failed fire is never refired immediately; the trigger's misfire policy governs recovery,
    /// and the deterministic ReportTrackingId makes a recovered fire safe.
    /// </remarks>
    [DisallowConcurrentExecution]
    public sealed class DmrpNightlyJob : IJob
    {
        public const string TimeZoneKey = "TimeZone";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IKafkaProducerFactory<string, object> _producerFactory;
        private readonly IOptions<DmrpSettings> _settings;
        private readonly IDmrpSchedulingMetrics _metrics;
        private readonly ILogger<DmrpNightlyJob> _logger;

        public DmrpNightlyJob(IServiceScopeFactory scopeFactory,
            IKafkaProducerFactory<string, object> producerFactory,
            IOptions<DmrpSettings> settings,
            IDmrpSchedulingMetrics metrics,
            ILogger<DmrpNightlyJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _producerFactory = producerFactory ?? throw new ArgumentNullException(nameof(producerFactory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var cancellationToken = context.CancellationToken;

            var zoneId = context.JobDetail.JobDataMap.GetString(TimeZoneKey)
                ?? throw new JobExecutionException($"{nameof(DmrpNightlyJob)} fired without a {TimeZoneKey} in its JobDataMap.");

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // The same fall-back as the reconciler: a zone the platform does not know is a job
                // that can never produce a correct period, so say so and fire nothing rather than
                // throw into Quartz's misfire handling every night.
                _logger.LogError(ex, "DMRP nightly fire for timezone {TimeZone} did nothing: the platform does not know it.", zoneId.SanitizeForLog());
                return;
            }

            var scheduling = _settings.Value.Scheduling;
            var scheduledUtc = context.ScheduledFireTimeUtc ?? context.FireTimeUtc;

            // The override is wall-clock time read in this zone, so one configured value is the same
            // local night for every zone job. Converted with the gap-aware helper so a value inside a
            // DST gap cannot throw.
            if (scheduling.ResolvedScheduledFireTimeOverride is { } overrideLocal)
            {
                var overrideUtc = new DateTimeOffset(ReportingPeriodMath.ToUtcAfterGap(overrideLocal, timeZone), TimeSpan.Zero);
                _logger.LogWarning(
                    "DMRP nightly job for zone {TimeZone} is using the configured scheduled-fire-time override {OverrideLocal:s} local ({OverrideUtc:O}) instead of the trigger's scheduled time {ScheduledFireTimeUtc:O}. This is a QA aid and must not be set in production.",
                    zoneId.SanitizeForLog(), overrideLocal, overrideUtc, scheduledUtc);

                scheduledUtc = overrideUtc;
            }
            else if (scheduling.ScheduledFireTimeOverrideIsInvalid)
            {
                _logger.LogWarning(
                    "DMRP nightly job for zone {TimeZone} is ignoring the configured scheduled-fire-time override '{Override}': it must be a local date and time with no offset, e.g. 2026-09-30T23:59:00.",
                    zoneId.SanitizeForLog(), scheduling.ScheduledFireTimeOverride.SanitizeForLog());
            }

            var comingMidnight = ReportingPeriods.ComingMidnight(scheduledUtc, timeZone, scheduling.ResolvedNightlyLocalTime);
            var periods = ReportingPeriods.StartingAt(comingMidnight);

            IReadOnlyList<ScheduledFacility> facilities;
            using (var scope = _scopeFactory.CreateScope())
            {
                facilities = await scope.ServiceProvider.GetRequiredService<IFacilityDirectory>()
                    .GetActiveInTimeZoneAsync(zoneId, cancellationToken);
            }

            _logger.LogInformation(
                "DMRP nightly fire for zone {TimeZone}: {FacilityCount} facilities, periods starting {ComingMidnight}: {Frequencies}",
                zoneId.SanitizeForLog(), facilities.Count, comingMidnight, string.Join(",", periods.Select(p => p.Frequency)));

            using var producer = _producerFactory.CreateProducer(new ProducerConfig());
            using var gate = new SemaphoreSlim(scheduling.ResolvedConcurrency);

            var outcomes = await Task.WhenAll(facilities.Select(async facility =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    return await RunFacilityAsync(facility, comingMidnight, periods, timeZone, scheduling, producer,
                        cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }));

            foreach (var outcome in outcomes)
            {
                _metrics.RecordFacilityOutcome(zoneId, outcome);
            }

            _logger.LogInformation(
                "DMRP nightly fire for zone {TimeZone} done: {Emitted} emitted, {Skipped} skipped, {Failed} failed",
                zoneId.SanitizeForLog(),
                outcomes.Count(o => o == DmrpFireOutcome.Emitted),
                outcomes.Count(o => o == DmrpFireOutcome.Skipped),
                outcomes.Count(o => o == DmrpFireOutcome.Failed));
        }

        private async Task<DmrpFireOutcome> RunFacilityAsync(ScheduledFacility facility, DateTime comingMidnight,
            IReadOnlyList<ScheduledPeriod> periods, TimeZoneInfo timeZone, DmrpSchedulingSettings scheduling,
            IProducer<string, object> producer, CancellationToken cancellationToken)
        {
            // Its own scope: the repositories behind these are EF-backed and not safe to share
            // across the facilities running concurrently.
            using var scope = _scopeFactory.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var backfilled = await RefreshIfDueAsync(facility.FacilityId, comingMidnight, scheduling, services,
                    cancellationToken);

                // A catch-up that just gave this facility its first rows for the month also announces
                // the monthly period already under way, so a failed month-end refresh costs the days
                // already passed rather than the whole month. Nothing is re-announced on other
                // nights: the rows were there, so their periods were announced when they started.
                var toAnnounce = backfilled
                    ? periods.Concat(ReportingPeriods.LateStartingBefore(comingMidnight)).ToList()
                    : periods;

                var source = services.GetRequiredService<IReportingPlanSource>();
                var projector = services.GetRequiredService<IReportingPlanScheduleProjector>();

                var emitted = 0;

                foreach (var period in toAnnounce)
                {
                    var entries = await source.GetForPeriodAsync(facility.FacilityId, period.LocalStart.Month,
                        period.LocalStart.Year, cancellationToken);

                    // The write path already warned about unmapped measures when the plan was saved.
                    var schedule = projector.Project(entries, facility.FacilityId,
                        new ReportingPeriod(period.LocalStart.Year, period.LocalStart.Month), warnOnUnmapped: false);

                    // Only Daily and Monthly are produced. A weekly mapping is not scheduled while
                    // DMRP is on; say so rather than drop it silently.
                    if (schedule.Weekly.Length > 0)
                    {
                        _logger.LogWarning(
                            "Facility {FacilityId} has weekly measure mapping(s) ({Dqms}); the DMRP nightly job does not produce weekly reports.",
                            facility.FacilityId.SanitizeForLog(), string.Join(",", schedule.Weekly).SanitizeForLog());
                    }

                    var dqms = period.Frequency switch
                    {
                        ReportingPeriodMath.Daily => schedule.Daily,
                        ReportingPeriodMath.Monthly => schedule.Monthly,
                        _ => []
                    };

                    if (dqms.Length == 0)
                    {
                        continue;
                    }

                    await ProduceAsync(producer, facility.FacilityId, period, dqms, timeZone, cancellationToken);
                    emitted++;
                }

                if (emitted == 0)
                {
                    _logger.LogInformation(
                        "Facility {FacilityId} has nothing to report for the periods starting {ComingMidnight}.",
                        facility.FacilityId.SanitizeForLog(), comingMidnight);

                    return DmrpFireOutcome.Skipped;
                }

                return DmrpFireOutcome.Emitted;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DMRP nightly scheduling failed for facility {FacilityId}.",
                    facility.FacilityId.SanitizeForLog());

                return DmrpFireOutcome.Failed;
            }
        }

        /// <summary>
        /// Month end: refresh the month that starts at the coming midnight. Early in a month: refresh
        /// a facility that has no rows at all for it, which repairs a failed month-end refresh and
        /// covers a facility onboarded after rollover. Bounded to the first CatchUpNights nights so
        /// facilities enrolled in nothing are not probed every night of the month.
        /// </summary>
        /// <returns>
        /// True when a catch-up refresh gave the facility rows for the month it had none for - the
        /// caller then announces the month's periods already under way.
        /// </returns>
        private async Task<bool> RefreshIfDueAsync(string facilityId, DateTime comingMidnight,
            DmrpSchedulingSettings scheduling, IServiceProvider services, CancellationToken cancellationToken)
        {
            var month = comingMidnight.Month;
            var year = comingMidnight.Year;

            // One predicate for both readings of the same question - has this facility any rows for
            // the month - so the "are we due a catch-up" test and the "did the catch-up land" test
            // cannot drift apart.
            Expression<Func<FacilityReportingPlan, bool>> hasRowsForTheMonth =
                p => p.FacilityId == facilityId && p.ReportingMonth == month && p.ReportingYear == year;

            var monthEnd = comingMidnight.Day == 1;
            var catchUp = false;

            IEntityRepository<FacilityReportingPlan>? plans = null;

            if (!monthEnd && comingMidnight.Day <= 1 + scheduling.ResolvedCatchUpNights)
            {
                plans = services.GetRequiredService<IEntityRepository<FacilityReportingPlan>>();

                catchUp = !await plans.AnyAsync(hasRowsForTheMonth, cancellationToken);
            }

            if (!monthEnd && !catchUp)
            {
                return false;
            }

            var sync = services.GetRequiredService<IDmrpReportingPlanSync>();
            var refreshed = false;

            for (var attempt = 1; attempt <= 2 && !refreshed; attempt++)
            {
                try
                {
                    await sync.SyncAsync(facilityId, month, year, cancellationToken);

                    refreshed = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt == 1)
                {
                    _logger.LogInformation(ex, "DMRP refresh for facility {FacilityId} ({Month}/{Year}) failed once; retrying.",
                        facilityId.SanitizeForLog(), month, year);
                }
                catch (Exception ex)
                {
                    // Contained: the fire goes on and emits from whatever rows exist. Repeated
                    // failures here are the signal to alert on.
                    _logger.LogWarning(ex, "DMRP refresh for facility {FacilityId} ({Month}/{Year}) failed; scheduling from stored rows.",
                        facilityId.SanitizeForLog(), month, year);

                    _metrics.RecordPlanRefreshFailure(facilityId);
                }
            }

            // Outside the retry, deliberately: a database error confirming the rows is neither a DMRP
            // call worth repeating nor a plan-refresh failure worth counting, and it belongs to the
            // facility's own error handling. Backfilled only if the refresh actually produced rows -
            // an empty answer from DMRP leaves nothing to announce, late or otherwise.
            return refreshed && catchUp && await plans!.AnyAsync(hasRowsForTheMonth, cancellationToken);
        }

        private async Task ProduceAsync(IProducer<string, object> producer, string facilityId, ScheduledPeriod period,
            string[] dqms, TimeZoneInfo timeZone, CancellationToken cancellationToken)
        {
            var (startUtc, endUtc) = ReportingPeriodMath.ForFrequency(period.Frequency, period.LocalStart, timeZone);
            var trackingId = ReportTrackingIds.For(facilityId, period.Frequency, startUtc).ToString();

            // Same key, header and value shape as the classic ReportScheduledJob, so downstream
            // consumers cannot tell which scheduler produced the event.
            var headers = new Headers
            {
                { "X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(trackingId) }
            };

            var message = new Message<string, object>
            {
                Key = facilityId,
                Headers = headers,
                Value = new ReportScheduledMessage
                {
                    ReportTypes = dqms,
                    Frequency = period.Frequency,
                    StartDate = startUtc,
                    EndDate = endUtc,
                    ReportTrackingId = trackingId
                }
            };

            await producer.ProduceAsync(KafkaTopic.ReportScheduled.ToString(), message, cancellationToken);

            _metrics.RecordReportScheduled(facilityId, period.Frequency, dqms, startUtc, endUtc);

            _logger.LogInformation(
                "Produced {Topic} for facility {FacilityId}: {Frequency} {StartDate} - {EndDate}, {DqmCount} dQM(s), tracking {TrackingId}",
                KafkaTopic.ReportScheduled, facilityId.SanitizeForLog(), period.Frequency, startUtc, endUtc, dqms.Length, trackingId);
        }
    }
}
