using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class QuartzJobHelperTests
{
    private sealed class TestJob : IJob
    {
        public System.Threading.Tasks.Task Execute(IJobExecutionContext context) => System.Threading.Tasks.Task.CompletedTask;
    }

    [Fact]
    public async System.Threading.Tasks.Task ScheduleJob_Schedules_WithExpectedIdentityTriggerAndData()
    {
        var logger = new Mock<ILogger<QuartzJobHelper>>();
        var scheduler = new Mock<IScheduler>();
        var schedulerFactory = new Mock<ISchedulerFactory>();

        IJobDetail? capturedJob = null;
        ITrigger? capturedTrigger = null;
        CancellationToken capturedToken = default;

        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .Callback<IJobDetail, ITrigger, CancellationToken>((job, trigger, token) =>
            {
                capturedJob = job;
                capturedTrigger = trigger;
                capturedToken = token;
            })
            .Returns((IJobDetail _, ITrigger __, CancellationToken ___) => System.Threading.Tasks.Task.FromResult(DateTimeOffset.UtcNow));

        var helper = new QuartzJobHelper(logger.Object, schedulerFactory.Object);
        var reportId = Guid.NewGuid();
        var facilityId = "Facility-A";
        var startAt = DateTimeOffset.UtcNow.AddMinutes(2);
        var token = new CancellationTokenSource().Token;

        await helper.ScheduleJob<TestJob>(
            new Dictionary<string, object>
            {
                ["ReportScheduleId"] = reportId,
                ["FacilityId"] = facilityId,
                ["Attempt"] = 1
            },
            startAt,
            identity: "job-1",
            group: "group-1",
            description: "desc-1",
            ct: token);

        Assert.NotNull(capturedJob);
        Assert.NotNull(capturedTrigger);

        Assert.Equal("job-1", capturedJob!.Key.Name);
        Assert.Equal("group-1", capturedJob.Key.Group);
        Assert.Equal("desc-1", capturedJob.Description);

        Assert.Equal("job-1-Trigger", capturedTrigger!.Key.Name);
        Assert.Equal("group-1", capturedTrigger.Key.Group);

        Assert.Equal(reportId, capturedJob.JobDataMap.GetObject<Guid?>("ReportScheduleId"));
        Assert.Equal(facilityId, capturedJob.JobDataMap.GetObject<string>("FacilityId"));
        Assert.Equal(1, capturedJob.JobDataMap.GetObject<int>("Attempt"));

        Assert.Equal(token, capturedToken);
    }

    [Fact]
    public async System.Threading.Tasks.Task ScheduleJob_UsesIdentityAsDescription_WhenDescriptionNull()
    {
        var logger = new Mock<ILogger<QuartzJobHelper>>();
        var scheduler = new Mock<IScheduler>();
        var schedulerFactory = new Mock<ISchedulerFactory>();

        IJobDetail? capturedJob = null;

        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .Callback<IJobDetail, ITrigger, CancellationToken>((job, _, _) => capturedJob = job)
            .Returns((IJobDetail _, ITrigger __, CancellationToken ___) => System.Threading.Tasks.Task.FromResult(DateTimeOffset.UtcNow));

        var helper = new QuartzJobHelper(logger.Object, schedulerFactory.Object);

        await helper.ScheduleJob<TestJob>(
            new Dictionary<string, object>(),
            DateTimeOffset.UtcNow,
            identity: "job-id",
            group: "job-group",
            description: null);

        Assert.NotNull(capturedJob);
        Assert.Equal("job-id", capturedJob!.Description);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteJob_DeletesExpectedJobKey()
    {
        var logger = new Mock<ILogger<QuartzJobHelper>>();
        var scheduler = new Mock<IScheduler>();
        var schedulerFactory = new Mock<ISchedulerFactory>();

        JobKey? capturedKey = null;
        CancellationToken capturedToken = default;

        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        scheduler
            .Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .Callback<JobKey, CancellationToken>((key, token) =>
            {
                capturedKey = key;
                capturedToken = token;
            })
            .ReturnsAsync(true);

        var helper = new QuartzJobHelper(logger.Object, schedulerFactory.Object);
        var token = new CancellationTokenSource().Token;

        await helper.DeleteJob("job-delete", "group-delete", token);

        Assert.NotNull(capturedKey);
        Assert.Equal("job-delete", capturedKey!.Name);
        Assert.Equal("group-delete", capturedKey.Group);
        Assert.Equal(token, capturedToken);
    }

    [Fact]
    public async System.Threading.Tasks.Task RescheduleJob_DeletesThenSchedulesNewTrigger()
    {
        var logger = new Mock<ILogger<QuartzJobHelper>>();
        var scheduler = new Mock<IScheduler>();
        var schedulerFactory = new Mock<ISchedulerFactory>();

        var calls = new List<string>();

        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        scheduler
            .Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("delete"))
            .ReturnsAsync(true);

        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("schedule"))
            .Returns((IJobDetail _, ITrigger __, CancellationToken ___) => System.Threading.Tasks.Task.FromResult(DateTimeOffset.UtcNow));

        var helper = new QuartzJobHelper(logger.Object, schedulerFactory.Object);
        var newStartAt = DateTimeOffset.UtcNow.AddMinutes(5);

        await helper.RescheduleJob<TestJob>(
            identity: "job-reschedule",
            jobData: new Dictionary<string, object> { ["ReportScheduleId"] = Guid.NewGuid() },
            newStartAt: newStartAt,
            group: "group-reschedule",
            description: "rescheduled");

        Assert.Equal(2, calls.Count);
        Assert.Equal("delete", calls[0]);
        Assert.Equal("schedule", calls[1]);

        schedulerFactory.Verify(f => f.GetScheduler(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async System.Threading.Tasks.Task ScheduleJob_PersistsGuidInJobDataMap_AndCanBeReadBack()
    {
        var logger = new Mock<ILogger<QuartzJobHelper>>();
        var scheduler = new Mock<IScheduler>();
        var schedulerFactory = new Mock<ISchedulerFactory>();

        IJobDetail? capturedJob = null;

        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .Callback<IJobDetail, ITrigger, CancellationToken>((job, _, _) => capturedJob = job)
            .Returns((IJobDetail _, ITrigger __, CancellationToken ___) => System.Threading.Tasks.Task.FromResult(DateTimeOffset.UtcNow));

        var helper = new QuartzJobHelper(logger.Object, schedulerFactory.Object);
        var id = Guid.NewGuid();

        await helper.ScheduleJob<TestJob>(
            new Dictionary<string, object> { ["ReportScheduleId"] = id },
            DateTimeOffset.UtcNow,
            identity: "job-guid",
            group: "group-guid");

        Assert.NotNull(capturedJob);
        Assert.Equal(id, capturedJob!.JobDataMap.GetObject<Guid?>("ReportScheduleId"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ScheduleJob_UsesProvidedCancellationToken_WhenResolvingSchedulerAndScheduling()
    {
        var logger = new Mock<ILogger<QuartzJobHelper>>();
        var scheduler = new Mock<IScheduler>();
        var schedulerFactory = new Mock<ISchedulerFactory>();

        CancellationToken schedulerFactoryToken = default;
        CancellationToken schedulerToken = default;

        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(t => schedulerFactoryToken = t)
            .ReturnsAsync(scheduler.Object);

        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .Callback<IJobDetail, ITrigger, CancellationToken>((_, _, t) => schedulerToken = t)
            .Returns((IJobDetail _, ITrigger __, CancellationToken ___) => System.Threading.Tasks.Task.FromResult(DateTimeOffset.UtcNow));

        var helper = new QuartzJobHelper(logger.Object, schedulerFactory.Object);
        var cts = new CancellationTokenSource();

        await helper.ScheduleJob<TestJob>(
            new Dictionary<string, object>(),
            DateTimeOffset.UtcNow,
            identity: "job-token",
            group: "group-token",
            ct: cts.Token);

        Assert.Equal(cts.Token, schedulerFactoryToken);
        Assert.Equal(cts.Token, schedulerToken);
    }
}
