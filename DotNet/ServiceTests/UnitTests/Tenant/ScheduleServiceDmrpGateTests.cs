using FluentAssertions;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Utilities;
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
        (await _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(ReportSchedulingJobs.ClassicJobGroup))).Count;

    [Fact]
    public async Task With_dmrp_off_a_facility_gets_its_classic_jobs()
    {
        var service = Create(dmrpEnabled: false);
        await service.StartAsync(CancellationToken.None);

        await service.AddJobsForFacility(MonthlyFacility());

        (await ClassicJobCount()).Should().Be(1);
    }

    /// <summary>
    /// With the flag on this service is not hosted - DmrpNightlyScheduleHostedService is - so StartAsync
    /// is never called. TenantFacilityOperations still calls these three on every facility save, which
    /// is why the gates inside them, rather than the missing registration, are what keeps Quartz clean.
    /// </summary>
    [Fact]
    public async Task With_dmrp_on_add_update_and_delete_touch_nothing()
    {
        var service = Create(dmrpEnabled: true);

        await service.AddJobsForFacility(MonthlyFacility());
        await service.UpdateJobsForFacility(MonthlyFacility(), MonthlyFacility());
        await service.DeleteJobsForFacility("100");

        (await ClassicJobCount()).Should().Be(0);
    }

    /// <summary>
    /// The other two public entry points. StartAsync is what assigns the scheduler field, and with the
    /// flag on it is never called, so an ungated method here would not quietly do nothing - it would
    /// throw a NullReferenceException on the first dereference.
    /// </summary>
    [Fact]
    public async Task With_dmrp_on_the_remaining_entry_points_do_not_reach_for_the_scheduler()
    {
        var service = Create(dmrpEnabled: true);

        await service.Invoking(s => s.DeleteJob("100")).Should().NotThrowAsync();
        await service.Invoking(s => s.GetAllJobs()).Should().NotThrowAsync();
    }
}
