using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Quartz.Impl.Matchers;
using ServiceTests.TestHelpers;
using UnitTests.Shared.Auditing;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers how the retention job and its trigger are created, rescheduled and removed in the Quartz store at
/// startup, against a real in-memory scheduler.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionScheduleReconcilerTests : IAsyncLifetime
{
    private static readonly JobKey JobKey =
        new(ReportConstants.RetentionScheduler.JobName, ReportConstants.RetentionScheduler.Group);

    private static readonly TriggerKey TriggerKey =
        new(ReportConstants.RetentionScheduler.JobName, ReportConstants.RetentionScheduler.Group);

    private readonly RetentionScheduleSettings _settings = new();
    private readonly CapturingLogger<RetentionScheduleReconciler> _logger = new();
    private ServiceProvider _provider = null!;
    private IScheduler _scheduler = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddQuartzTestLogging();

        // Quartz keeps schedulers in a process-wide registry by name, so a unique name keeps this class from
        // sharing a scheduler with other test classes that run in parallel.
        services.AddQuartz(q =>
        {
            q.SchedulerName = $"retention-reconciler-tests-{Guid.NewGuid():N}";
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

    [Fact]
    public async Task Reconcile_EmptyStore_CreatesDurableRecoverableJobWithUtcCronTrigger()
    {
        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        var job = await _scheduler.GetJobDetail(JobKey);
        Assert.NotNull(job);
        Assert.Equal(typeof(RetentionCheckJob), job.JobType);
        Assert.True(job.Durable);
        Assert.True(job.RequestsRecovery);

        var trigger = await SingleCronTriggerAsync();
        Assert.Equal(TriggerKey, trigger.Key);
        Assert.Equal(RetentionScheduleSettings.DefaultCron, trigger.CronExpressionString);
        Assert.Equal(TimeZoneInfo.Utc.Id, trigger.TimeZone.Id);
        Assert.Equal(MisfireInstruction.CronTrigger.FireOnceNow, trigger.MisfireInstruction);
    }

    [Fact]
    public async Task Reconcile_EmptyStore_LogsCronAndNextFireTime()
    {
        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        var nextFire = (await SingleCronTriggerAsync()).GetNextFireTimeUtc();
        Assert.NotNull(nextFire);

        var entry = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains(RetentionScheduleSettings.DefaultCron, entry.Message);
        Assert.Contains("UTC", entry.Message);
        Assert.Contains(nextFire.Value.ToString("O"), entry.Message);
    }

    [Fact]
    public async Task Reconcile_AlreadyScheduled_LogsCronAndNextFireTime()
    {
        await CreateReconciler().ReconcileAsync(CancellationToken.None);
        _logger.Entries.Clear();

        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        var nextFire = (await SingleCronTriggerAsync()).GetNextFireTimeUtc();
        Assert.NotNull(nextFire);

        var entry = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains(RetentionScheduleSettings.DefaultCron, entry.Message);
        Assert.Contains("UTC", entry.Message);
        Assert.Contains(nextFire.Value.ToString("O"), entry.Message);
    }

    [Fact]
    public async Task Reconcile_RunTwice_LeavesOneJobAndOneTrigger()
    {
        var reconciler = CreateReconciler();

        await reconciler.ReconcileAsync(CancellationToken.None);
        await reconciler.ReconcileAsync(CancellationToken.None);

        Assert.Single(await _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(JobKey.Group)));
        await SingleCronTriggerAsync();
    }

    [Fact]
    public async Task Reconcile_LowercaseNamesUnchanged_DoesNotReschedule()
    {
        // Quartz stores cron expressions uppercased, so "sat,sun" comes back as "SAT,SUN".
        _settings.Cron = "0 0 2 ? * sat,sun";
        await CreateReconciler().ReconcileAsync(CancellationToken.None);
        _logger.Entries.Clear();

        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        Assert.Equal("0 0 2 ? * SAT,SUN", (await SingleCronTriggerAsync()).CronExpressionString);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("Rescheduled"));
        Assert.Contains(_logger.Entries, e => e.Message.Contains("already scheduled"));
    }

    [Fact]
    public async Task Reconcile_CronChanged_ReschedulesExistingTrigger()
    {
        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        _settings.Cron = "0 30 4 * * ?";
        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        Assert.Equal("0 30 4 * * ?", (await SingleCronTriggerAsync()).CronExpressionString);
        Assert.Contains(_logger.Entries,
                        e => e.Level == LogLevel.Information &&
                             e.Message.Contains(RetentionScheduleSettings.DefaultCron) &&
                             e.Message.Contains("0 30 4 * * ?"));
    }

    [Fact]
    public async Task Reconcile_TriggerNotInUtc_ReschedulesToUtc()
    {
        await _scheduler.ScheduleJob(CreateStoredJob(),
                                     CreateStoredTrigger(RetentionScheduleSettings.DefaultCron, "Pacific/Guam"));

        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        Assert.Equal(TimeZoneInfo.Utc.Id, (await SingleCronTriggerAsync()).TimeZone.Id);
    }

    [Fact]
    public async Task Reconcile_InvalidCron_LogsErrorAndUsesDefault()
    {
        _settings.Cron = "every night please";

        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        Assert.Equal(RetentionScheduleSettings.DefaultCron, (await SingleCronTriggerAsync()).CronExpressionString);

        var error = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("every night please", error.Message);
        Assert.Contains(RetentionScheduleSettings.DefaultCron, error.Message);
    }

    [Fact]
    public async Task Reconcile_Disabled_RemovesJobAndTrigger()
    {
        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        _settings.Enabled = false;
        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        Assert.False(await _scheduler.CheckExists(JobKey));
        Assert.False(await _scheduler.CheckExists(TriggerKey));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("removed"));
    }

    [Fact]
    public async Task Reconcile_DisabledWithNoJob_DoesNothing()
    {
        _settings.Enabled = false;

        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        Assert.False(await _scheduler.CheckExists(JobKey));
        Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Reconcile_JobWithoutTrigger_LogsWarningAndLeavesStoreAlone()
    {
        await _scheduler.AddJob(CreateStoredJob(), replace: false);

        await CreateReconciler().ReconcileAsync(CancellationToken.None);

        Assert.True(await _scheduler.CheckExists(JobKey));
        Assert.Empty(await _scheduler.GetTriggersOfJob(JobKey));
        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Reconcile_LosesCreateRace_VerifiesTriggerInsteadOfThrowing()
    {
        // Another instance stored the job and trigger after this one checked the store.
        await CreateReconciler().ReconcileAsync(CancellationToken.None);
        var reconciler = new RetentionScheduleReconciler(StaleReadSchedulerFactory(),
                                                         Options.Create(_settings),
                                                         _logger);

        var exception = await Record.ExceptionAsync(() => reconciler.ReconcileAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(RetentionScheduleSettings.DefaultCron, (await SingleCronTriggerAsync()).CronExpressionString);
    }

    private RetentionScheduleReconciler CreateReconciler() =>
        new(_provider.GetRequiredService<ISchedulerFactory>(), Options.Create(_settings), _logger);

    private async Task<ICronTrigger> SingleCronTriggerAsync()
    {
        var triggers = await _scheduler.GetTriggersOfJob(JobKey);

        return Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(triggers));
    }

    private static IJobDetail CreateStoredJob() =>
        JobBuilder.Create<RetentionCheckJob>()
            .WithIdentity(JobKey)
            .StoreDurably()
            .RequestRecovery()
            .Build();

    private static ITrigger CreateStoredTrigger(string cron, string timeZoneId) =>
        TriggerBuilder.Create()
            .ForJob(JobKey)
            .WithIdentity(TriggerKey)
            .WithCronSchedule(cron, x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)))
            .Build();

    // Answers CheckExists with false, as if this instance read the store before another one stored the job,
    // and passes every other call the reconciler is expected to make to the real scheduler. A strict mock
    // fails the test if the reconciler calls anything else.
    private ISchedulerFactory StaleReadSchedulerFactory()
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
        scheduler
            .Setup(s => s.GetTrigger(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()))
            .Returns((TriggerKey k, CancellationToken c) => _scheduler.GetTrigger(k, c));

        var factory = new Mock<ISchedulerFactory>();
        factory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        return factory.Object;
    }
}
