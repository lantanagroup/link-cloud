using FluentAssertions;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Spi;
using ServiceTests.TestHelpers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant;

[Trait("Category", "UnitTests")]
public class ScheduleServiceDmrpGateTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider _provider = null!;
    private IScheduler _scheduler = null!;

    public async Task InitializeAsync()
    {
        _connection.Open();

        var services = new ServiceCollection();
        services.AddQuartzTestLogging();
        services.RegisterQuartzDatabaseInTest();
        // StartAsync with DMRP off enumerates facilities through TenantDbContext; an empty one is enough.
        services.AddDbContext<TenantDbContext>(o => o.UseSqlite(_connection));
        _provider = services.BuildServiceProvider();

        using (var scope = _provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.EnsureCreated();
        }

        _scheduler = await _provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
    }

    public async Task DisposeAsync()
    {
        await _scheduler.Shutdown();
        await _provider.DisposeAsync();
        _connection.Dispose();
    }

    private ScheduleService Create(bool dmrpEnabled) => new(NullLogger<ScheduleService>.Instance,
        _provider.GetRequiredService<ISchedulerFactory>(), _provider.GetRequiredService<IServiceScopeFactory>(),
        new Mock<IJobFactory>().Object, Options.Create(new DmrpSettings { Enabled = dmrpEnabled }));

    private static Facility MonthlyFacility() => new()
    {
        FacilityId = "100",
        FacilityName = "Test",
        TimeZone = "UTC",
        ScheduledReports = new ScheduledReportModel { Daily = [], Weekly = [], Monthly = ["NHSNdQMHTCDI"] }
    };

    private async Task<int> ClassicJobCount() =>
        (await _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(KafkaTopic.ReportScheduled)))).Count;

    [Fact]
    public async Task With_dmrp_off_a_facility_gets_its_classic_jobs()
    {
        var service = Create(dmrpEnabled: false);
        await service.StartAsync(CancellationToken.None);

        await service.AddJobsForFacility(MonthlyFacility());

        (await ClassicJobCount()).Should().Be(1);
    }

    [Fact]
    public async Task With_dmrp_on_add_update_and_delete_touch_nothing()
    {
        var service = Create(dmrpEnabled: true);
        await service.StartAsync(CancellationToken.None);

        await service.AddJobsForFacility(MonthlyFacility());
        await service.UpdateJobsForFacility(MonthlyFacility(), MonthlyFacility());
        await service.DeleteJobsForFacility("100");

        (await ClassicJobCount()).Should().Be(0);
    }

    [Fact]
    public async Task With_dmrp_on_boot_removes_classic_jobs_left_from_before_the_flag()
    {
        // A classic per-facility job as ScheduleService created it before the flag was turned on.
        await _scheduler.AddJob(JobBuilder.Create<LantanaGroup.Link.Tenant.Jobs.ReportScheduledJob>()
            .WithIdentity("100-Monthly", nameof(KafkaTopic.ReportScheduled)).StoreDurably().Build(), replace: false);
        (await ClassicJobCount()).Should().Be(1);

        await Create(dmrpEnabled: true).StartAsync(CancellationToken.None);

        (await ClassicJobCount()).Should().Be(0);
    }
}
