using FluentAssertions;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Spi;
using ServiceTests.TestHelpers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP;

[Trait("Category", "UnitTests")]
public class DmrpNightlyJobReconcilerTests : IAsyncLifetime
{
    private readonly Mock<IFacilityDirectory> _directory = new();
    private readonly DmrpSettings _settings = new() { Enabled = true };
    private ServiceProvider _provider = null!;
    private IScheduler _scheduler = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddQuartzTestLogging();
        services.RegisterQuartzDatabaseInTest();
        services.AddScoped(_ => _directory.Object);
        _provider = services.BuildServiceProvider();
        _scheduler = await _provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
    }

    public async Task DisposeAsync()
    {
        await _scheduler.Shutdown();
        await _provider.DisposeAsync();
    }

    private DmrpNightlyJobReconciler CreateReconciler()
    {
        var monitor = new Mock<IOptionsMonitor<DmrpSettings>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(_settings);

        return new DmrpNightlyJobReconciler(_provider.GetRequiredService<ISchedulerFactory>(),
            _provider.GetRequiredService<IServiceScopeFactory>(), monitor.Object,
            NullLogger<DmrpNightlyJobReconciler>.Instance);
    }

    private void Zones(params string[] zones) =>
        _directory.Setup(d => d.GetTimeZonesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(zones.ToList());

    private async Task<IReadOnlyCollection<JobKey>> ZoneJobs() =>
        await _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(DmrpNightlyJobReconciler.JobGroup));

    [Fact]
    public async Task Ensure_creates_one_durable_job_per_zone_and_is_idempotent()
    {
        var reconciler = CreateReconciler();

        await reconciler.EnsureZoneJobAsync("America/Chicago");
        await reconciler.EnsureZoneJobAsync("America/Chicago");

        var keys = await ZoneJobs();
        keys.Should().ContainSingle().Which.Name.Should().Be("America/Chicago");

        var detail = await _scheduler.GetJobDetail(keys.Single());
        detail!.Durable.Should().BeTrue();
        detail.JobDataMap.GetString(DmrpNightlyJob.TimeZoneKey).Should().Be("America/Chicago");

        var trigger = (await _scheduler.GetTriggersOfJob(keys.Single())).Single().As<ICronTrigger>();
        trigger.CronExpressionString.Should().Be("0 59 23 * * ?");
        trigger.TimeZone.Id.Should().Be(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago").Id);
    }

    [Fact]
    public async Task Ensure_reschedules_when_the_cron_changes()
    {
        var reconciler = CreateReconciler();
        await reconciler.EnsureZoneJobAsync("UTC");

        _settings.Scheduling.NightlyCron = "0 0 21 * * ?";
        await reconciler.EnsureZoneJobAsync("UTC");

        var trigger = (await _scheduler.GetTriggersOfJob(new JobKey("UTC", DmrpNightlyJobReconciler.JobGroup))).Single().As<ICronTrigger>();
        trigger.CronExpressionString.Should().Be("0 0 21 * * ?");
    }

    [Fact]
    public async Task Ensure_skips_a_zone_the_platform_does_not_know()
    {
        await CreateReconciler().EnsureZoneJobAsync("Mars/Olympus_Mons");

        (await ZoneJobs()).Should().BeEmpty();
    }

    [Fact]
    public async Task Reconcile_all_adds_missing_zones_and_removes_orphans()
    {
        var reconciler = CreateReconciler();
        await reconciler.EnsureZoneJobAsync("Pacific/Guam");

        Zones("America/Chicago", "America/New_York");
        await reconciler.ReconcileAllAsync();

        (await ZoneJobs()).Select(k => k.Name).Should().BeEquivalentTo(["America/Chicago", "America/New_York"]);
    }

    /// <summary>
    /// A directory read that comes back empty for a transient reason must not unschedule the fleet.
    /// Zero zones next to jobs that exist is far more likely a failed read than every facility in the
    /// estate being deleted at once, and the sweep is only ever a tidy-up: a zone job with no
    /// facilities produces nothing when it fires.
    /// </summary>
    [Fact]
    public async Task Reconcile_all_keeps_the_zone_jobs_when_the_directory_returns_nothing()
    {
        var reconciler = CreateReconciler();
        await reconciler.EnsureZoneJobAsync("America/Chicago");

        Zones();
        await reconciler.ReconcileAllAsync();

        (await ZoneJobs()).Select(k => k.Name).Should().BeEquivalentTo(["America/Chicago"]);
    }

    [Fact]
    public async Task Remove_all_deletes_every_zone_job_and_nothing_else()
    {
        var reconciler = CreateReconciler();
        await reconciler.EnsureZoneJobAsync("UTC");
        await _scheduler.AddJob(JobBuilder.Create<DmrpNightlyJob>().WithIdentity("other", "SomeoneElse").StoreDurably().Build(), replace: false);

        await reconciler.RemoveAllAsync();

        (await ZoneJobs()).Should().BeEmpty();
        (await _scheduler.CheckExists(new JobKey("other", "SomeoneElse"))).Should().BeTrue();
    }

    /// <summary>
    /// A durable job can exist with no trigger at all - e.g. a previous boot stored the job but was
    /// killed before it could schedule the trigger. Ensure must notice the missing trigger and
    /// install one rather than treating "the job exists" as "there is nothing to do".
    /// </summary>
    [Fact]
    public async Task Ensure_installs_a_trigger_for_a_durable_job_that_has_none()
    {
        var jobKey = new JobKey("America/Denver", DmrpNightlyJobReconciler.JobGroup);
        var job = JobBuilder.Create<DmrpNightlyJob>()
            .WithIdentity(jobKey)
            .UsingJobData(DmrpNightlyJob.TimeZoneKey, "America/Denver")
            .StoreDurably()
            .Build();
        await _scheduler.AddJob(job, replace: false);

        (await _scheduler.GetTriggersOfJob(jobKey)).Should().BeEmpty("the test assumes the job starts with no trigger");

        var reconciler = CreateReconciler();
        var act = async () => await reconciler.EnsureZoneJobAsync("America/Denver");

        await act.Should().NotThrowAsync();

        var triggers = (await _scheduler.GetTriggersOfJob(jobKey)).OfType<ICronTrigger>().ToList();
        triggers.Should().ContainSingle();
        triggers.Single().TimeZone.Id.Should().Be(TimeZoneInfo.FindSystemTimeZoneById("America/Denver").Id);
    }

    /// <summary>
    /// Two cluster nodes racing EnsureZoneJobAsync for the same new zone: both see the job missing,
    /// both build it, and the loser's ScheduleJob call fails with ObjectAlreadyExistsException after
    /// the winner's has already landed. The loser must not crash the caller (the hosted service's
    /// StartAsync, for one) - it should fall through to the verify/reschedule path and find the
    /// winner's job already correctly configured.
    /// </summary>
    [Fact]
    public async Task Ensure_survives_losing_the_create_race_to_another_node()
    {
        var raceFactory = new RaceSimulatingSchedulerFactory(_provider.GetRequiredService<ISchedulerFactory>());

        var reconciler = new DmrpNightlyJobReconciler(raceFactory,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            OptionsMonitorFor(_settings),
            NullLogger<DmrpNightlyJobReconciler>.Instance);

        var act = async () => await reconciler.EnsureZoneJobAsync("Europe/London");

        await act.Should().NotThrowAsync();

        var jobKey = new JobKey("Europe/London", DmrpNightlyJobReconciler.JobGroup);
        (await _scheduler.CheckExists(jobKey)).Should().BeTrue();

        var triggers = (await _scheduler.GetTriggersOfJob(jobKey)).OfType<ICronTrigger>().ToList();
        triggers.Should().ContainSingle();
        triggers.Single().TimeZone.Id.Should().Be(TimeZoneInfo.FindSystemTimeZoneById("Europe/London").Id);
    }

    private static IOptionsMonitor<DmrpSettings> OptionsMonitorFor(DmrpSettings settings)
    {
        var monitor = new Mock<IOptionsMonitor<DmrpSettings>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(settings);
        return monitor.Object;
    }

    /// <summary>
    /// Wraps the real scheduler so the first call to the two-argument <c>ScheduleJob</c> overload -
    /// the one EnsureZoneJobAsync uses to create a brand-new zone job - schedules for real (standing
    /// in for the other node that won the race) and then reports the ObjectAlreadyExistsException the
    /// loser's own attempt would have received from Quartz. Every other member, and every later call,
    /// passes straight through.
    /// </summary>
    private sealed class RaceSimulatingScheduler(IScheduler inner) : IScheduler
    {
        private bool _raced;

        public async Task<DateTimeOffset> ScheduleJob(IJobDetail jobDetail, ITrigger trigger,
            CancellationToken cancellationToken = default)
        {
            if (!_raced)
            {
                _raced = true;
                await inner.ScheduleJob(jobDetail, trigger, cancellationToken);
                throw new ObjectAlreadyExistsException(
                    $"Simulated race: job {jobDetail.Key} was scheduled by another node between CheckExists and ScheduleJob.");
            }

            return await inner.ScheduleJob(jobDetail, trigger, cancellationToken);
        }

        public string SchedulerName => inner.SchedulerName;
        public string SchedulerInstanceId => inner.SchedulerInstanceId;
        public SchedulerContext Context => inner.Context;
        public bool InStandbyMode => inner.InStandbyMode;
        public bool IsShutdown => inner.IsShutdown;
        public IJobFactory JobFactory { set => inner.JobFactory = value; }
        public IListenerManager ListenerManager => inner.ListenerManager;
        public bool IsStarted => inner.IsStarted;

        public Task<bool> IsJobGroupPaused(string groupName, CancellationToken cancellationToken = default) =>
            inner.IsJobGroupPaused(groupName, cancellationToken);

        public Task<bool> IsTriggerGroupPaused(string groupName, CancellationToken cancellationToken = default) =>
            inner.IsTriggerGroupPaused(groupName, cancellationToken);

        public Task<SchedulerMetaData> GetMetaData(CancellationToken cancellationToken = default) =>
            inner.GetMetaData(cancellationToken);

        public Task<IReadOnlyCollection<IJobExecutionContext>> GetCurrentlyExecutingJobs(
            CancellationToken cancellationToken = default) => inner.GetCurrentlyExecutingJobs(cancellationToken);

        public Task<IReadOnlyCollection<string>> GetJobGroupNames(CancellationToken cancellationToken = default) =>
            inner.GetJobGroupNames(cancellationToken);

        public Task<IReadOnlyCollection<string>> GetTriggerGroupNames(CancellationToken cancellationToken = default) =>
            inner.GetTriggerGroupNames(cancellationToken);

        public Task<IReadOnlyCollection<string>> GetPausedTriggerGroups(CancellationToken cancellationToken = default) =>
            inner.GetPausedTriggerGroups(cancellationToken);

        public Task Start(CancellationToken cancellationToken = default) => inner.Start(cancellationToken);

        public Task StartDelayed(TimeSpan delay, CancellationToken cancellationToken = default) =>
            inner.StartDelayed(delay, cancellationToken);

        public Task Standby(CancellationToken cancellationToken = default) => inner.Standby(cancellationToken);

        public Task Shutdown(CancellationToken cancellationToken = default) => inner.Shutdown(cancellationToken);

        public Task Shutdown(bool waitForJobsToComplete, CancellationToken cancellationToken = default) =>
            inner.Shutdown(waitForJobsToComplete, cancellationToken);

        public Task<DateTimeOffset> ScheduleJob(ITrigger trigger, CancellationToken cancellationToken = default) =>
            inner.ScheduleJob(trigger, cancellationToken);

        public Task ScheduleJobs(IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>> triggersAndJobs,
            bool replace, CancellationToken cancellationToken = default) =>
            inner.ScheduleJobs(triggersAndJobs, replace, cancellationToken);

        public Task ScheduleJob(IJobDetail jobDetail, IReadOnlyCollection<ITrigger> triggersForJob, bool replace,
            CancellationToken cancellationToken = default) =>
            inner.ScheduleJob(jobDetail, triggersForJob, replace, cancellationToken);

        public Task<bool> UnscheduleJob(TriggerKey triggerKey, CancellationToken cancellationToken = default) =>
            inner.UnscheduleJob(triggerKey, cancellationToken);

        public Task<bool> UnscheduleJobs(IReadOnlyCollection<TriggerKey> triggerKeys,
            CancellationToken cancellationToken = default) => inner.UnscheduleJobs(triggerKeys, cancellationToken);

        public Task<DateTimeOffset?> RescheduleJob(TriggerKey triggerKey, ITrigger newTrigger,
            CancellationToken cancellationToken = default) =>
            inner.RescheduleJob(triggerKey, newTrigger, cancellationToken);

        public Task AddJob(IJobDetail jobDetail, bool replace, CancellationToken cancellationToken = default) =>
            inner.AddJob(jobDetail, replace, cancellationToken);

        public Task AddJob(IJobDetail jobDetail, bool replace, bool storeNonDurableWhileAwaitingScheduling,
            CancellationToken cancellationToken = default) =>
            inner.AddJob(jobDetail, replace, storeNonDurableWhileAwaitingScheduling, cancellationToken);

        public Task<bool> DeleteJob(JobKey jobKey, CancellationToken cancellationToken = default) =>
            inner.DeleteJob(jobKey, cancellationToken);

        public Task<bool> DeleteJobs(IReadOnlyCollection<JobKey> jobKeys, CancellationToken cancellationToken = default) =>
            inner.DeleteJobs(jobKeys, cancellationToken);

        public Task TriggerJob(JobKey jobKey, CancellationToken cancellationToken = default) =>
            inner.TriggerJob(jobKey, cancellationToken);

        public Task TriggerJob(JobKey jobKey, JobDataMap data, CancellationToken cancellationToken = default) =>
            inner.TriggerJob(jobKey, data, cancellationToken);

        public Task PauseJob(JobKey jobKey, CancellationToken cancellationToken = default) =>
            inner.PauseJob(jobKey, cancellationToken);

        public Task PauseJobs(GroupMatcher<JobKey> matcher, CancellationToken cancellationToken = default) =>
            inner.PauseJobs(matcher, cancellationToken);

        public Task PauseTrigger(TriggerKey triggerKey, CancellationToken cancellationToken = default) =>
            inner.PauseTrigger(triggerKey, cancellationToken);

        public Task PauseTriggers(GroupMatcher<TriggerKey> matcher, CancellationToken cancellationToken = default) =>
            inner.PauseTriggers(matcher, cancellationToken);

        public Task ResumeJob(JobKey jobKey, CancellationToken cancellationToken = default) =>
            inner.ResumeJob(jobKey, cancellationToken);

        public Task ResumeJobs(GroupMatcher<JobKey> matcher, CancellationToken cancellationToken = default) =>
            inner.ResumeJobs(matcher, cancellationToken);

        public Task ResumeTrigger(TriggerKey triggerKey, CancellationToken cancellationToken = default) =>
            inner.ResumeTrigger(triggerKey, cancellationToken);

        public Task ResumeTriggers(GroupMatcher<TriggerKey> matcher, CancellationToken cancellationToken = default) =>
            inner.ResumeTriggers(matcher, cancellationToken);

        public Task PauseAll(CancellationToken cancellationToken = default) => inner.PauseAll(cancellationToken);

        public Task ResumeAll(CancellationToken cancellationToken = default) => inner.ResumeAll(cancellationToken);

        public Task<IReadOnlyCollection<JobKey>> GetJobKeys(GroupMatcher<JobKey> matcher,
            CancellationToken cancellationToken = default) => inner.GetJobKeys(matcher, cancellationToken);

        public Task<IReadOnlyCollection<ITrigger>> GetTriggersOfJob(JobKey jobKey,
            CancellationToken cancellationToken = default) => inner.GetTriggersOfJob(jobKey, cancellationToken);

        public Task<IReadOnlyCollection<TriggerKey>> GetTriggerKeys(GroupMatcher<TriggerKey> matcher,
            CancellationToken cancellationToken = default) => inner.GetTriggerKeys(matcher, cancellationToken);

        public Task<IJobDetail?> GetJobDetail(JobKey jobKey, CancellationToken cancellationToken = default) =>
            inner.GetJobDetail(jobKey, cancellationToken);

        public Task<ITrigger?> GetTrigger(TriggerKey triggerKey, CancellationToken cancellationToken = default) =>
            inner.GetTrigger(triggerKey, cancellationToken);

        public Task<TriggerState> GetTriggerState(TriggerKey triggerKey, CancellationToken cancellationToken = default) =>
            inner.GetTriggerState(triggerKey, cancellationToken);

        public Task ResetTriggerFromErrorState(TriggerKey triggerKey, CancellationToken cancellationToken = default) =>
            inner.ResetTriggerFromErrorState(triggerKey, cancellationToken);

        public Task AddCalendar(string calName, ICalendar calendar, bool replace, bool updateTriggers,
            CancellationToken cancellationToken = default) =>
            inner.AddCalendar(calName, calendar, replace, updateTriggers, cancellationToken);

        public Task<bool> DeleteCalendar(string calName, CancellationToken cancellationToken = default) =>
            inner.DeleteCalendar(calName, cancellationToken);

        public Task<ICalendar?> GetCalendar(string calName, CancellationToken cancellationToken = default) =>
            inner.GetCalendar(calName, cancellationToken);

        public Task<IReadOnlyCollection<string>> GetCalendarNames(CancellationToken cancellationToken = default) =>
            inner.GetCalendarNames(cancellationToken);

        public Task<bool> Interrupt(JobKey jobKey, CancellationToken cancellationToken = default) =>
            inner.Interrupt(jobKey, cancellationToken);

        public Task<bool> Interrupt(string fireInstanceId, CancellationToken cancellationToken = default) =>
            inner.Interrupt(fireInstanceId, cancellationToken);

        public Task<bool> CheckExists(JobKey jobKey, CancellationToken cancellationToken = default) =>
            inner.CheckExists(jobKey, cancellationToken);

        public Task<bool> CheckExists(TriggerKey triggerKey, CancellationToken cancellationToken = default) =>
            inner.CheckExists(triggerKey, cancellationToken);

        public Task Clear(CancellationToken cancellationToken = default) => inner.Clear(cancellationToken);
    }

    /// <summary>
    /// Delegates every call to the real factory's scheduler except that the returned
    /// <see cref="IScheduler"/> is wrapped in a <see cref="RaceSimulatingScheduler"/>.
    /// </summary>
    private sealed class RaceSimulatingSchedulerFactory(ISchedulerFactory inner) : ISchedulerFactory
    {
        public async Task<IReadOnlyList<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default) =>
            await inner.GetAllSchedulers(cancellationToken);

        public async Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default) =>
            new RaceSimulatingScheduler(await inner.GetScheduler(cancellationToken));

        public async Task<IScheduler?> GetScheduler(string schedName, CancellationToken cancellationToken = default)
        {
            var scheduler = await inner.GetScheduler(schedName, cancellationToken);
            return scheduler is null ? null : new RaceSimulatingScheduler(scheduler);
        }
    }
}
