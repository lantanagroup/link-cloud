using System.Linq.Expressions;
using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP;

[Trait("Category", "UnitTests")]
public class DmrpNightlyJobTests
{
    private const string Zone = "UTC";
    private const string Dqm = "NHSNdQMAcuteCareHospitalInitialPopulation";

    private readonly Mock<IFacilityDirectory> _directory = new();
    private readonly Mock<IDmrpReportingPlanSync> _sync = new();
    private readonly Mock<IReportingPlanSource> _source = new();
    private readonly Mock<IEntityRepository<FacilityReportingPlan>> _plans = new();
    private readonly Mock<IDmrpSchedulingMetrics> _metrics = new();
    private readonly Mock<IProducer<string, object>> _producer = new();
    private readonly List<Message<string, object>> _produced = [];
    private readonly List<string> _topics = [];
    private readonly DmrpSettings _settings = new() { Enabled = true };

    public DmrpNightlyJobTests()
    {
        _producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, object>, CancellationToken>((topic, m, _) =>
            {
                lock (_produced)
                {
                    _topics.Add(topic);
                    _produced.Add(m);
                }
            })
            .ReturnsAsync((DeliveryResult<string, object>)null!);

        // Default world: one facility, rows exist for every month, mapped to one daily dQM.
        Facilities("100");
        RowsExist(true);
        _source
            .Setup(s => s.GetForPeriodAsync("100", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReportingPlanEntry("HOB", Dqm, Frequency.Daily)]);
    }

    private void Facilities(params string[] ids) =>
        _directory
            .Setup(d => d.GetActiveInTimeZoneAsync(Zone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids.Select(id => new ScheduledFacility(id, Zone)).ToList());

    private void RowsExist(bool exist) =>
        _plans
            .Setup(p => p.AnyAsync(It.IsAny<Expression<Func<FacilityReportingPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exist);

    private DmrpNightlyJob CreateJob()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _directory.Object);
        services.AddScoped(_ => _sync.Object);
        services.AddScoped(_ => _source.Object);
        services.AddScoped(_ => _plans.Object);
        services.AddScoped<IReportingPlanScheduleProjector>(_ =>
            new ReportingPlanScheduleProjector(NullLogger<ReportingPlanScheduleProjector>.Instance));
        var provider = services.BuildServiceProvider();

        var factory = new Mock<IKafkaProducerFactory<string, object>>();
        factory
            .Setup(f => f.CreateProducer(It.IsAny<ProducerConfig>(), null, null, true))
            .Returns(_producer.Object);

        return new DmrpNightlyJob(provider.GetRequiredService<IServiceScopeFactory>(), factory.Object,
            Options.Create(_settings), _metrics.Object, NullLogger<DmrpNightlyJob>.Instance);
    }

    private static IJobExecutionContext ContextFiredAt(DateTimeOffset scheduledUtc, string zone = Zone)
    {
        var detail = JobBuilder.Create<DmrpNightlyJob>()
            .WithIdentity(zone, "DmrpNightly")
            .UsingJobData(DmrpNightlyJob.TimeZoneKey, zone)
            .Build();

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.JobDetail).Returns(detail);
        context.SetupGet(c => c.ScheduledFireTimeUtc).Returns(scheduledUtc);
        context.SetupGet(c => c.FireTimeUtc).Returns(scheduledUtc.AddSeconds(2));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private static readonly DateTimeOffset LastNightOfOctober = new(2026, 10, 31, 23, 59, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NightOfOctober14 = new(2026, 10, 14, 23, 59, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NightOfNovember2 = new(2026, 11, 2, 23, 59, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NightOfNovember10 = new(2026, 11, 10, 23, 59, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_last_night_of_a_month_refreshes_next_month_before_emitting()
    {
        _source
            .Setup(s => s.GetForPeriodAsync("100", 11, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ReportingPlanEntry("HOB", Dqm, Frequency.Daily),
                new ReportingPlanEntry("HTCDI", "NHSNdQMHTCDI", Frequency.Monthly)
            ]);

        await CreateJob().Execute(ContextFiredAt(LastNightOfOctober));

        _sync.Verify(s => s.SyncAsync("100", 11, 2026, It.IsAny<CancellationToken>()), Times.Once);

        // 2026-11-01 is the first of the month, so Daily and Monthly both start there.
        _produced.Select(m => ((ReportScheduledMessage)m.Value).Frequency)
            .Should().BeEquivalentTo([ReportingPeriodMath.Daily, ReportingPeriodMath.Monthly]);

        var monthly = (ReportScheduledMessage)_produced.Single(m => ((ReportScheduledMessage)m.Value).Frequency == "Monthly").Value;
        monthly.ReportTypes.Should().BeEquivalentTo(["NHSNdQMHTCDI"]);
        monthly.StartDate.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        monthly.EndDate.Should().Be(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(-1));
        _produced.Should().OnlyContain(m => m.Key == "100");

        // Everything downstream keys off these two halves agreeing: the Report service dedupes on the
        // ReportTrackingId in the value, and every other service correlates on the header. The classic
        // job writes one value into both, and so must this one.
        _produced.Should().AllSatisfy(m =>
            Encoding.ASCII.GetString(m.Headers.GetLastBytes("X-Correlation-Id"))
                .Should().Be(((ReportScheduledMessage)m.Value).ReportTrackingId));

        _topics.Should().OnlyContain(t => t == KafkaTopic.ReportScheduled.ToString()).And.HaveCount(2);

        // One per produced event, the counterpart of the classic job's per-event counter.
        _metrics.Verify(m => m.RecordReportScheduled("100", It.IsAny<string>(), It.IsAny<string[]>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Exactly(2));
        _metrics.Verify(m => m.RecordReportScheduled("100", ReportingPeriodMath.Monthly,
            new[] { "NHSNdQMHTCDI" }, monthly.StartDate, monthly.EndDate), Times.Once);
    }

    [Fact]
    public async Task A_mid_month_night_with_rows_does_not_call_dmrp()
    {
        // Also covers the override-unset case: with ScheduledFireTimeOverride left at its default
        // (null), a mid-month fire behaves exactly as here - Daily only, no sync call.
        await CreateJob().Execute(ContextFiredAt(NightOfOctober14));

        _sync.Verify(s => s.SyncAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _produced.Should().ContainSingle().Which.Value.As<ReportScheduledMessage>().Frequency.Should().Be("Daily");
    }

    [Fact]
    public async Task A_scheduled_fire_time_override_rehearses_month_end_on_a_mid_month_fire()
    {
        // 23:59 local on Oct 31 - the QA override anchors the periods there even though the trigger
        // itself fired mid-month.
        _settings.Scheduling.ScheduledFireTimeOverride = "2026-10-31T23:59:00";
        _source
            .Setup(s => s.GetForPeriodAsync("100", 11, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ReportingPlanEntry("HOB", Dqm, Frequency.Daily),
                new ReportingPlanEntry("HTCDI", "NHSNdQMHTCDI", Frequency.Monthly)
            ]);

        await CreateJob().Execute(ContextFiredAt(NightOfOctober14));

        _sync.Verify(s => s.SyncAsync("100", 11, 2026, It.IsAny<CancellationToken>()), Times.Once);
        _produced.Select(m => ((ReportScheduledMessage)m.Value).Frequency)
            .Should().BeEquivalentTo([ReportingPeriodMath.Daily, ReportingPeriodMath.Monthly]);
        _produced.Should().AllSatisfy(m =>
            ((ReportScheduledMessage)m.Value).StartDate.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task The_override_is_read_as_wall_clock_time_in_the_zone_jobs_own_timezone()
    {
        // The same value means a different instant per zone: 23:59 on Oct 31 in New York is
        // 03:59Z on Nov 1 (EDT, the clocks change an hour later that night), so the periods it
        // anchors start at Nov 1 00:00 Eastern = 04:00Z.
        const string newYork = "America/New_York";
        _directory
            .Setup(d => d.GetActiveInTimeZoneAsync(newYork, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ScheduledFacility("100", newYork)]);
        _settings.Scheduling.ScheduledFireTimeOverride = "2026-10-31T23:59:00";

        await CreateJob().Execute(ContextFiredAt(NightOfOctober14, newYork));

        _sync.Verify(s => s.SyncAsync("100", 11, 2026, It.IsAny<CancellationToken>()), Times.Once);
        _produced.Should().AllSatisfy(m =>
            ((ReportScheduledMessage)m.Value).StartDate.Should().Be(new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task An_override_carrying_an_offset_is_ignored_not_guessed()
    {
        // A UTC instant would be a different local night in every zone, so it is refused and the
        // fire behaves as if no override were set: mid-month, Daily only, no DMRP call.
        _settings.Scheduling.ScheduledFireTimeOverride = "2026-11-01T03:59:00Z";

        await CreateJob().Execute(ContextFiredAt(NightOfOctober14));

        _sync.Verify(s => s.SyncAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _produced.Should().ContainSingle().Which.Value.As<ReportScheduledMessage>().Frequency.Should().Be("Daily");
    }

    [Fact]
    public async Task An_unparseable_scheduled_fire_time_override_is_ignored()
    {
        _settings.Scheduling.ScheduledFireTimeOverride = "not a date";

        await CreateJob().Execute(ContextFiredAt(NightOfOctober14));

        _sync.Verify(s => s.SyncAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _produced.Should().ContainSingle().Which.Value.As<ReportScheduledMessage>().Frequency.Should().Be("Daily");
    }

    [Fact]
    public async Task Catch_up_refreshes_a_facility_with_no_rows_on_the_first_nights_only()
    {
        RowsExist(false);

        await CreateJob().Execute(ContextFiredAt(NightOfNovember2));
        _sync.Verify(s => s.SyncAsync("100", 11, 2026, It.IsAny<CancellationToken>()), Times.Once);

        _sync.Invocations.Clear();

        await CreateJob().Execute(ContextFiredAt(NightOfNovember10));
        _sync.Verify(s => s.SyncAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_catch_up_that_lands_announces_the_month_late()
    {
        // No November rows until the catch-up sync writes them.
        var rows = false;
        _plans
            .Setup(p => p.AnyAsync(It.IsAny<Expression<Func<FacilityReportingPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => rows);
        _sync
            .Setup(s => s.SyncAsync("100", 11, 2026, It.IsAny<CancellationToken>()))
            .Callback(() => rows = true)
            .ReturnsAsync(new DmrpSyncResult(2, 0, 0, 0));
        _source
            .Setup(s => s.GetForPeriodAsync("100", 11, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ReportingPlanEntry("HOB", Dqm, Frequency.Daily),
                new ReportingPlanEntry("HTCDI", "NHSNdQMHTCDI", Frequency.Monthly)
            ]);

        // Night of Monday Nov 2; coming midnight Nov 3. The month began Sunday Nov 1.
        await CreateJob().Execute(ContextFiredAt(NightOfNovember2));

        var values = _produced.Select(m => (ReportScheduledMessage)m.Value).ToList();
        values.Select(v => v.Frequency).Should().BeEquivalentTo([ReportingPeriodMath.Daily, ReportingPeriodMath.Monthly]);
        values.Single(v => v.Frequency == "Daily").StartDate.Should().Be(new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));
        values.Single(v => v.Frequency == "Monthly").StartDate.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task A_catch_up_that_finds_nothing_announces_nothing_late()
    {
        RowsExist(false);
        _sync
            .Setup(s => s.SyncAsync("100", 11, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DmrpSyncResult.Nothing);
        _source
            .Setup(s => s.GetForPeriodAsync("100", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReportingPlanEntry>());

        await CreateJob().Execute(ContextFiredAt(NightOfNovember2));

        _produced.Should().BeEmpty();
        _metrics.Verify(m => m.RecordFacilityOutcome(Zone, DmrpFireOutcome.Skipped), Times.Once);
    }

    [Fact]
    public async Task A_failed_refresh_is_retried_once_then_emission_continues_from_stored_rows()
    {
        _sync
            .Setup(s => s.SyncAsync("100", 11, 2026, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DMRP down"));

        await CreateJob().Execute(ContextFiredAt(LastNightOfOctober));

        _sync.Verify(s => s.SyncAsync("100", 11, 2026, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _metrics.Verify(m => m.RecordPlanRefreshFailure("100"), Times.Once);
        _produced.Should().NotBeEmpty();
        _metrics.Verify(m => m.RecordFacilityOutcome(Zone, DmrpFireOutcome.Emitted), Times.Once);
    }

    [Fact]
    public async Task One_facility_failing_does_not_stop_the_others()
    {
        Facilities("100", "200");
        _source
            .Setup(s => s.GetForPeriodAsync("200", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await CreateJob().Execute(ContextFiredAt(NightOfOctober14));

        _produced.Should().ContainSingle().Which.Key.Should().Be("100");
        _metrics.Verify(m => m.RecordFacilityOutcome(Zone, DmrpFireOutcome.Emitted), Times.Once);
        _metrics.Verify(m => m.RecordFacilityOutcome(Zone, DmrpFireOutcome.Failed), Times.Once);
    }

    [Fact]
    public async Task A_weekly_mapping_is_not_produced()
    {
        _source
            .Setup(s => s.GetForPeriodAsync("100", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ReportingPlanEntry("HOB", Dqm, Frequency.Daily),
                new ReportingPlanEntry("SSI", "NHSNdQMSSI", Frequency.Weekly)
            ]);

        // 2026-10-10 is a Saturday, so the coming midnight is a Sunday; the classic scheduler would
        // have fired a weekly job here. The nightly job does not.
        await CreateJob().Execute(ContextFiredAt(new DateTimeOffset(2026, 10, 10, 23, 59, 0, TimeSpan.Zero)));

        _produced.Select(m => ((ReportScheduledMessage)m.Value).Frequency).Should().BeEquivalentTo([ReportingPeriodMath.Daily]);
        _produced.Should().OnlyContain(m => !((ReportScheduledMessage)m.Value).ReportTypes.Contains("NHSNdQMSSI"));
    }

    [Fact]
    public async Task A_facility_with_nothing_to_announce_is_skipped_not_failed()
    {
        _source
            .Setup(s => s.GetForPeriodAsync("100", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReportingPlanEntry>());

        await CreateJob().Execute(ContextFiredAt(NightOfOctober14));

        _produced.Should().BeEmpty();
        _metrics.Verify(m => m.RecordFacilityOutcome(Zone, DmrpFireOutcome.Skipped), Times.Once);
    }

    [Fact]
    public async Task The_tracking_id_is_the_same_when_a_night_is_fired_twice()
    {
        await CreateJob().Execute(ContextFiredAt(NightOfOctober14));
        await CreateJob().Execute(ContextFiredAt(NightOfOctober14));

        _produced.Select(m => ((ReportScheduledMessage)m.Value).ReportTrackingId).Distinct().Should().ContainSingle();
    }

    /// <summary>
    /// A zone job whose timezone the platform cannot resolve has no period math it could get right.
    /// It fires nothing and says so, rather than throwing into Quartz's misfire handling every night.
    /// </summary>
    [Fact]
    public async Task A_zone_the_platform_does_not_know_fires_nothing_and_does_not_throw()
    {
        var act = () => CreateJob().Execute(ContextFiredAt(NightOfOctober14, "Mars/Olympus_Mons"));

        await act.Should().NotThrowAsync();
        _produced.Should().BeEmpty();
        _metrics.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Concurrency_is_bounded_by_the_setting()
    {
        _settings.Scheduling.Concurrency = 2;
        Facilities("1", "2", "3", "4", "5", "6");
        var inFlight = 0;
        var peak = 0;
        _source
            .Setup(s => s.GetForPeriodAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                peak = Math.Max(peak, Interlocked.Increment(ref inFlight));
                await Task.Delay(20);
                Interlocked.Decrement(ref inFlight);
                return [new ReportingPlanEntry("HOB", Dqm, Frequency.Daily)];
            });

        await CreateJob().Execute(ContextFiredAt(NightOfOctober14));

        peak.Should().BeLessThanOrEqualTo(2);
        _produced.Should().HaveCount(6);
    }
}
