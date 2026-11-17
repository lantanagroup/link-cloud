using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Quartz;
using ServiceTests.TestHelpers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared;

/// <summary>
/// Covers the shared helper that keeps one durable job with one cron trigger in a Quartz store, used by the
/// Report retention schedule and the DMRP nightly zone jobs.
/// </summary>
[Trait("Category", "UnitTests")]
public class CronJobReconcilerTests : IAsyncLifetime
{
    private static readonly JobKey JobKey = new("cron-job", "CronJobReconcilerTests");
    private static readonly TriggerKey TriggerKey = new("cron-job", "CronJobReconcilerTests");

    private ServiceProvider _provider = null!;
    private IScheduler _scheduler = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddQuartzTestLogging();

        // Quartz keeps schedulers in a process-wide registry by name; a unique name keeps this class's
        // scheduler apart from other test classes running in parallel.
        services.AddQuartz(q =>
        {
            q.SchedulerName = $"cron-job-reconciler-tests-{Guid.NewGuid():N}";
            q.UseInMemoryStore();
        });

        _provider = services.BuildServiceProvider();
        _scheduler = await _provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
    }

    public async Task DisposeAsync()
    {
        await _scheduler.Shutdown();
        await _provider.DisposeAsync();
    }

    [Theory]
    [InlineData("0 0 2 * * ?")]
    [InlineData("0 0 2 ? * sat,sun")]
    [InlineData("0 0 2 * * ? 2099")]
    public void CanFire_ValidFutureCron_ReturnsTrue(string cron)
    {
        Assert.True(CronJobReconciler.CanFire(cron, TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("every night")]
    [InlineData("0 0 25 * * ?")]
    [InlineData("0 0 2 1 1 ? 2020")]
    public void CanFire_InvalidOrPastCron_ReturnsFalse(string? cron)
    {
        Assert.False(CronJobReconciler.CanFire(cron, TimeZoneInfo.Utc));
    }

    [Fact]
    public async Task EnsureAsync_EmptyStore_CreatesDurableJobWithOneCronTrigger()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

        var result = await EnsureAsync(Definition() with
        {
            TimeZone = timeZone,
            RequestRecovery = true,
            Description = "a test job",
            JobData = new Dictionary<string, string> { ["zone"] = "America/Chicago" }
        });

        Assert.Equal(CronJobReconcileOutcome.Created, result.Outcome);

        var job = await _scheduler.GetJobDetail(JobKey);
        Assert.NotNull(job);
        Assert.Equal(typeof(NoopJob), job.JobType);
        Assert.True(job.Durable);
        Assert.True(job.RequestsRecovery);
        Assert.Equal("a test job", job.Description);
        Assert.Equal("America/Chicago", job.JobDataMap.GetString("zone"));

        var trigger = await SingleCronTriggerAsync();
        Assert.Equal(TriggerKey, trigger.Key);
        Assert.Equal("0 0 2 * * ?", trigger.CronExpressionString);
        Assert.Equal(timeZone.Id, trigger.TimeZone.Id);
        Assert.Equal(MisfireInstruction.CronTrigger.FireOnceNow, trigger.MisfireInstruction);
        Assert.Equal(trigger.GetNextFireTimeUtc(), result.NextFireTimeUtc);
    }

    [Fact]
    public async Task EnsureAsync_RequestRecoveryOff_StoresJobWithoutRecovery()
    {
        await EnsureAsync(Definition());

        var job = await _scheduler.GetJobDetail(JobKey);
        Assert.False(job!.RequestsRecovery);
    }

    [Fact]
    public async Task EnsureAsync_RunTwice_ReturnsUnchangedAndKeepsOneTrigger()
    {
        await EnsureAsync(Definition());

        var result = await EnsureAsync(Definition());

        Assert.Equal(CronJobReconcileOutcome.Unchanged, result.Outcome);
        Assert.NotNull(result.NextFireTimeUtc);
        await SingleCronTriggerAsync();
    }

    [Fact]
    public async Task EnsureAsync_LowercaseNames_ReturnsUnchangedOnSecondRun()
    {
        await EnsureAsync(Definition() with { Cron = "0 0 2 ? * sat,sun" });

        var result = await EnsureAsync(Definition() with { Cron = "0 0 2 ? * sat,sun" });

        Assert.Equal(CronJobReconcileOutcome.Unchanged, result.Outcome);
        Assert.Equal("0 0 2 ? * SAT,SUN", result.Cron);
    }

    [Fact]
    public async Task EnsureAsync_CronChanged_ReschedulesAndReportsPreviousCron()
    {
        await EnsureAsync(Definition());

        var result = await EnsureAsync(Definition() with { Cron = "0 30 4 * * ?" });

        Assert.Equal(CronJobReconcileOutcome.Rescheduled, result.Outcome);
        Assert.Equal("0 0 2 * * ?", result.PreviousCron);
        Assert.Equal("0 30 4 * * ?", (await SingleCronTriggerAsync()).CronExpressionString);
    }

    [Fact]
    public async Task EnsureAsync_TimeZoneChanged_Reschedules()
    {
        await EnsureAsync(Definition());
        var guam = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Guam");

        var result = await EnsureAsync(Definition() with { TimeZone = guam });

        Assert.Equal(CronJobReconcileOutcome.Rescheduled, result.Outcome);
        Assert.Equal(guam.Id, (await SingleCronTriggerAsync()).TimeZone.Id);
    }

    [Fact]
    public async Task EnsureAsync_JobWithoutTrigger_AddsTrigger()
    {
        await _scheduler.AddJob(JobBuilder.Create<NoopJob>().WithIdentity(JobKey).StoreDurably().Build(),
                                replace: false);

        var result = await EnsureAsync(Definition());

        Assert.Equal(CronJobReconcileOutcome.TriggerAdded, result.Outcome);
        Assert.Equal("0 0 2 * * ?", (await SingleCronTriggerAsync()).CronExpressionString);
    }

    [Fact]
    public async Task EnsureAsync_LosesCreateRace_VerifiesInsteadOfThrowing()
    {
        // Another instance stored the job and trigger after this one checked the store.
        await EnsureAsync(Definition());

        var result = await CronJobReconciler.EnsureAsync(StaleReadScheduler(), Definition(), CancellationToken.None);

        Assert.Equal(CronJobReconcileOutcome.Unchanged, result.Outcome);
        await SingleCronTriggerAsync();
    }

    private static CronJobDefinition Definition() =>
        new()
        {
            JobKey = JobKey,
            JobType = typeof(NoopJob),
            Cron = "0 0 2 * * ?",
            TimeZone = TimeZoneInfo.Utc
        };

    private Task<CronJobReconcileResult> EnsureAsync(CronJobDefinition definition) =>
        CronJobReconciler.EnsureAsync(_scheduler, definition, CancellationToken.None);

    private async Task<ICronTrigger> SingleCronTriggerAsync()
    {
        var triggers = await _scheduler.GetTriggersOfJob(JobKey);

        return Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(triggers));
    }

    // Answers CheckExists with false, as if read before another instance stored the job, and passes the other
    // calls the create race takes to the real scheduler. A strict mock fails the test on anything else.
    private IScheduler StaleReadScheduler()
    {
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        scheduler
            .Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .Returns((IJobDetail j, ITrigger t, CancellationToken c) => _scheduler.ScheduleJob(j, t, c));
        scheduler
            .Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .Returns((JobKey k, CancellationToken c) => _scheduler.GetTriggersOfJob(k, c));

        return scheduler.Object;
    }

    private sealed class NoopJob : IJob
    {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }
}
