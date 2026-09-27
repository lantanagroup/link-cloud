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
using Quartz.Listener;
using ServiceTests.TestHelpers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP;

/// <summary>
/// With DMRP enabled the host does not host its classic <c>ScheduleService</c> at all, so these two
/// services are the whole of what happens to the shared Quartz scheduler at boot.
/// </summary>
[Trait("Category", "UnitTests")]
public class DmrpNightlyScheduleHostedServiceTests : IAsyncLifetime
{
    /// <summary>
    /// Stands in for the host's classic job group. A name only this test uses, because the point is
    /// that the service sweeps whatever group it was handed.
    /// </summary>
    private const string ClassicGroup = "HostClassicReportJobs";

    private readonly Mock<IFacilityDirectory> _directory = new();
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

        Zones();
    }

    public async Task DisposeAsync()
    {
        if (!_scheduler.IsShutdown)
        {
            await _scheduler.Shutdown();
        }

        await _provider.DisposeAsync();
    }

    private void Zones(params string[] zones) =>
        _directory.Setup(d => d.GetTimeZonesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(zones.ToList());

    private DmrpNightlyJobReconciler CreateReconciler()
    {
        var monitor = new Mock<IOptionsMonitor<DmrpSettings>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(new DmrpSettings { Enabled = true });

        return new DmrpNightlyJobReconciler(_provider.GetRequiredService<ISchedulerFactory>(),
            _provider.GetRequiredService<IServiceScopeFactory>(), monitor.Object,
            NullLogger<DmrpNightlyJobReconciler>.Instance);
    }

    private DmrpNightlyScheduleHostedService CreateHostedService() =>
        new(CreateReconciler(), _provider.GetRequiredService<ISchedulerFactory>(),
            new DmrpSchedulingHostOptions(ClassicGroup),
            NullLogger<DmrpNightlyScheduleHostedService>.Instance);

    /// <summary>A durable, triggerless job standing in for one the host left in its classic group.</summary>
    private Task AddClassicJob(string name = "100-Monthly") =>
        _scheduler.AddJob(JobBuilder.Create<NoOpJob>().WithIdentity(name, ClassicGroup).StoreDurably().Build(),
            replace: false);

    private async Task<int> ClassicJobCount() =>
        (await _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(ClassicGroup))).Count;

    private async Task<IReadOnlyCollection<JobKey>> ZoneJobs() =>
        await _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(DmrpNightlyJobReconciler.JobGroup));

    [Fact]
    public async Task Start_sweeps_the_classic_group_and_leaves_a_job_for_every_zone()
    {
        Zones("America/Chicago", "UTC");
        await AddClassicJob();
        (await ClassicJobCount()).Should().Be(1);

        await CreateHostedService().StartAsync(CancellationToken.None);

        (await ClassicJobCount()).Should().Be(0);
        (await ZoneJobs()).Select(k => k.Name).Should().BeEquivalentTo("America/Chicago", "UTC");
    }

    [Fact]
    public async Task Start_starts_the_shared_scheduler_and_stop_shuts_it_down()
    {
        Zones("UTC");
        var service = CreateHostedService();
        _scheduler.IsStarted.Should().BeFalse();

        await service.StartAsync(CancellationToken.None);
        _scheduler.IsStarted.Should().BeTrue();

        await service.StopAsync(CancellationToken.None);
        _scheduler.IsShutdown.Should().BeTrue();
    }

    /// <summary>
    /// The order is the safety property: a classic job left from before the flag must not get a
    /// window in which it could fire against a measure list the module no longer maintains. Asserted
    /// from inside Quartz's own <c>SchedulerStarting</c> callback rather than after the fact, so a
    /// future reordering of StartAsync fails here instead of passing.
    /// </summary>
    [Fact]
    public async Task Start_sweeps_the_classic_group_before_the_scheduler_is_started()
    {
        Zones("UTC");
        await AddClassicJob();

        var listener = new ClassicGroupWatcher(_scheduler);
        _scheduler.ListenerManager.AddSchedulerListener(listener);

        await CreateHostedService().StartAsync(CancellationToken.None);

        listener.ClassicJobsWhenStarting.Should().Be(0);
    }

    /// <summary>
    /// With the flag off the host hosts its own <c>ScheduleService</c>, which owns the scheduler's
    /// start and shutdown. The cleanup service only sweeps what the module left behind.
    /// </summary>
    [Fact]
    public async Task Cleanup_removes_the_zone_jobs_and_touches_neither_the_classic_group_nor_the_scheduler()
    {
        Zones("UTC");
        await CreateReconciler().ReconcileAllAsync();
        await AddClassicJob();
        (await ZoneJobs()).Should().HaveCount(1);

        var cleanup = new DmrpNightlyScheduleCleanupService(CreateReconciler());
        await cleanup.StartAsync(CancellationToken.None);

        (await ZoneJobs()).Should().BeEmpty();
        (await ClassicJobCount()).Should().Be(1);
        _scheduler.IsStarted.Should().BeFalse();

        await cleanup.StopAsync(CancellationToken.None);

        _scheduler.IsShutdown.Should().BeFalse();
    }

    /// <summary>
    /// Records how many classic jobs survive to the moment Quartz first begins starting. The first
    /// notification only: Quartz notifies on every <c>Start</c> call, and a later, redundant one would
    /// otherwise overwrite the observation with a count taken after the sweep had run.
    /// </summary>
    private sealed class ClassicGroupWatcher : SchedulerListenerSupport
    {
        private readonly IScheduler _scheduler;

        public ClassicGroupWatcher(IScheduler scheduler) => _scheduler = scheduler;

        public int? ClassicJobsWhenStarting { get; private set; }

        public override async Task SchedulerStarting(CancellationToken cancellationToken = default)
        {
            var count = (await _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(ClassicGroup), cancellationToken)).Count;

            ClassicJobsWhenStarting ??= count;
        }
    }

    private sealed class NoOpJob : IJob
    {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }
}
