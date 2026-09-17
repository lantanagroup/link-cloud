using FluentAssertions;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Quartz.Impl.Matchers;
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
        // A disposable ILoggerFactory from AddLogging() gets torn down by this test's DisposeAsync,
        // but Quartz's logging bridge holds a process-wide static reference to whichever factory the
        // first Quartz scheduler in the process was built with. A later test's scheduler creation
        // then throws ObjectDisposedException reaching through that stale reference. NullLoggerFactory
        // is a shared instance whose Dispose() is a no-op, so it satisfies Quartz's requirement for an
        // ILoggerFactory without that cross-test hazard.
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
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
}
