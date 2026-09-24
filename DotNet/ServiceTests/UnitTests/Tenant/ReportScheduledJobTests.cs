using Confluent.Kafka;
using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Jobs;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant;

[Trait("Category", "UnitTests")]
public class ReportScheduledJobTests
{
    private static readonly TimeZoneInfo Chicago = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    private readonly Mock<IProducer<string, object>> _producer = new();
    private readonly List<Message<string, object>> _produced = [];
    private readonly List<string> _topics = [];
    private readonly Mock<ITenantServiceMetrics> _metrics = new();

    public ReportScheduledJobTests()
    {
        _producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, object>, CancellationToken>((topic, m, _) =>
            {
                _topics.Add(topic);
                _produced.Add(m);
            })
            .ReturnsAsync((DeliveryResult<string, object>)null!);
    }

    private ReportScheduledJob CreateJob()
    {
        var factory = new Mock<IKafkaProducerFactory<string, object>>();
        factory
            .Setup(f => f.CreateProducer(It.IsAny<ProducerConfig>(), null, null, true))
            .Returns(_producer.Object);

        return new ReportScheduledJob(NullLogger<ReportScheduledJob>.Instance, factory.Object, _metrics.Object);
    }

    private static Facility BuildFacility(string facilityId, string[] daily, string[] weekly, string[] monthly) =>
        new()
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = Chicago.Id,
            ScheduledReports = new ScheduledReportModel
            {
                Daily = daily,
                Weekly = weekly,
                Monthly = monthly
            }
        };

    /// <summary>
    /// Mirrors ScheduleService's own CreateJob/CreateTrigger: the facility and frequency go into the
    /// job's JobDataMap, and the cron string that fired it goes into the trigger's, all through the
    /// same JobDataMapExtensions the classic scheduler uses.
    /// </summary>
    private static IJobExecutionContext ContextFor(Facility facility, string frequency, DateTimeOffset scheduledUtc,
        string cron = "0 0 0 * * ? *")
    {
        var jobDataMap = new JobDataMap();
        jobDataMap.PutObject(TenantConstants.Scheduler.Facility, facility);
        jobDataMap.PutObject(TenantConstants.Scheduler.Frequency, frequency);

        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(j => j.JobDataMap).Returns(jobDataMap);

        var triggerDataMap = new JobDataMap();
        triggerDataMap.PutObject(TenantConstants.Scheduler.JobTrigger, cron);

        var trigger = new Mock<ITrigger>();
        trigger.SetupGet(t => t.JobDataMap).Returns(triggerDataMap);

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(c => c.Trigger).Returns(trigger.Object);
        context.SetupGet(c => c.ScheduledFireTimeUtc).Returns(scheduledUtc);
        context.SetupGet(c => c.FireTimeUtc).Returns(scheduledUtc.AddSeconds(2));
        return context.Object;
    }

    [Fact]
    public async Task Monthly_fire_uses_the_monthly_reports_and_period()
    {
        var facility = BuildFacility("100", daily: ["DailyMeasure"], weekly: ["WeeklyMeasure"], monthly: ["MonthlyMeasure"]);
        // 2026-11-01 00:00 CST is 06:00 UTC.
        var scheduledUtc = new DateTimeOffset(2026, 11, 1, 6, 0, 0, TimeSpan.Zero);

        await CreateJob().Execute(ContextFor(facility, ScheduleService.MONTHLY, scheduledUtc));

        _produced.Should().ContainSingle();
        var message = (ReportScheduledMessage)_produced.Single().Value;
        message.Frequency.Should().Be(ScheduleService.MONTHLY);
        message.ReportTypes.Should().BeEquivalentTo(facility.ScheduledReports.Monthly);

        var localDate = TimeZoneInfo.ConvertTime(scheduledUtc, Chicago).DateTime;
        var (expectedStart, expectedEnd) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Monthly, localDate, Chicago);
        message.StartDate.Should().Be(expectedStart);
        message.EndDate.Should().Be(expectedEnd);

        _topics.Should().ContainSingle().Which.Should().Be(KafkaTopic.ReportScheduled.ToString());
        _metrics.Verify(m => m.IncrementReportScheduledCounter(It.IsAny<List<KeyValuePair<string, object?>>>()), Times.Once);
    }

    [Fact]
    public async Task Weekly_fire_uses_the_weekly_reports_and_period()
    {
        var facility = BuildFacility("100", daily: ["DailyMeasure"], weekly: ["WeeklyMeasure"], monthly: ["MonthlyMeasure"]);
        // 2026-11-08 00:00 CST (a Sunday) is 06:00 UTC.
        var scheduledUtc = new DateTimeOffset(2026, 11, 8, 6, 0, 0, TimeSpan.Zero);

        await CreateJob().Execute(ContextFor(facility, ScheduleService.WEEKLY, scheduledUtc));

        var message = (ReportScheduledMessage)_produced.Single().Value;
        message.Frequency.Should().Be(ScheduleService.WEEKLY);
        message.ReportTypes.Should().BeEquivalentTo(facility.ScheduledReports.Weekly);

        var localDate = TimeZoneInfo.ConvertTime(scheduledUtc, Chicago).DateTime;
        var (expectedStart, expectedEnd) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Weekly, localDate, Chicago);
        message.StartDate.Should().Be(expectedStart);
        message.EndDate.Should().Be(expectedEnd);
    }

    [Fact]
    public async Task Daily_fire_uses_the_daily_reports_and_period()
    {
        var facility = BuildFacility("100", daily: ["DailyMeasure"], weekly: ["WeeklyMeasure"], monthly: ["MonthlyMeasure"]);
        // 2026-11-10 00:00 CST is 06:00 UTC.
        var scheduledUtc = new DateTimeOffset(2026, 11, 10, 6, 0, 0, TimeSpan.Zero);

        await CreateJob().Execute(ContextFor(facility, ScheduleService.DAILY, scheduledUtc));

        var message = (ReportScheduledMessage)_produced.Single().Value;
        message.Frequency.Should().Be(ScheduleService.DAILY);
        message.ReportTypes.Should().BeEquivalentTo(facility.ScheduledReports.Daily);

        var localDate = TimeZoneInfo.ConvertTime(scheduledUtc, Chicago).DateTime;
        var (expectedStart, expectedEnd) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Daily, localDate, Chicago);
        message.StartDate.Should().Be(expectedStart);
        message.EndDate.Should().Be(expectedEnd);
    }

    /// <summary>
    /// ReportScheduledJob.Execute reads an unrecognized frequency's report types as an empty array
    /// (no throw), but then hands the same frequency to ReportingPeriodMath.ForFrequency, which does
    /// throw for anything other than Daily/Weekly/Monthly. Inside the job, that throw is caught by a
    /// handler that calls Thread.Sleep(600000) - ten minutes - before refiring, which is not
    /// something a unit test should trigger. This asserts the pre-catch behaviour directly instead of
    /// executing the job end to end for this case.
    /// </summary>
    [Fact]
    public void An_unsupported_frequency_has_no_period_math()
    {
        var act = () => ReportingPeriodMath.ForFrequency("Hourly", new DateTime(2026, 11, 10), Chicago);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
