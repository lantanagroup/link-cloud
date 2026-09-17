using FluentAssertions;
using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Tenant.Business;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant;

/// <summary>
/// The host's answer to "where are my facilities", which the DMRP module reads both a single
/// facility's timezone and the nightly job's facility enumeration through. Run against a real
/// <see cref="TenantDbContext"/>, because the distinctions that matter - deleted versus not,
/// blank versus absent - are decided by the query.
/// </summary>
[Trait("Category", "UnitTests")]
public class TenantFacilityDirectoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TenantFacilityDirectoryTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private TenantDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>().UseSqlite(_connection).Options;
        var context = new TenantDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static Facility Facility(string id, string timeZone, bool deleted = false) => new()
    {
        FacilityId = id,
        FacilityName = $"Facility {id}",
        TimeZone = timeZone,
        IsDeleted = deleted,
        ScheduledReports = new ScheduledReportModel { Daily = [], Weekly = [], Monthly = [] }
    };

    [Fact]
    public async Task Time_zones_are_distinct_and_exclude_deleted_and_blank()
    {
        await using var context = CreateContext();
        context.Facilities.AddRange(
            Facility("1", "America/Chicago"),
            Facility("2", "America/Chicago"),
            Facility("3", "America/New_York"),
            Facility("4", "Pacific/Guam", deleted: true),
            Facility("5", ""));
        await context.SaveChangesAsync();

        var directory = new TenantFacilityDirectory(new EntityRepository<Facility, TenantDbContext>(context));

        var zones = await directory.GetTimeZonesAsync();

        zones.Should().BeEquivalentTo(["America/Chicago", "America/New_York"]);
    }

    [Fact]
    public async Task Facilities_in_a_zone_exclude_deleted_ones()
    {
        await using var context = CreateContext();
        context.Facilities.AddRange(
            Facility("1", "America/Chicago"),
            Facility("2", "America/Chicago", deleted: true),
            Facility("3", "America/New_York"));
        await context.SaveChangesAsync();

        var directory = new TenantFacilityDirectory(new EntityRepository<Facility, TenantDbContext>(context));

        var facilities = await directory.GetActiveInTimeZoneAsync("America/Chicago");

        facilities.Should().ContainSingle().Which.Should().Be(new ScheduledFacility("1", "America/Chicago"));
    }

    /// <summary>
    /// Deliberately different from #1918's <c>TenantFacilityTimeZoneSource</c>, which did not exclude
    /// soft-deleted facilities. A deleted facility should not anchor the nightly job's reporting period.
    /// </summary>
    [Fact]
    public async Task GetTimeZoneAsync_does_not_return_a_deleted_facilitys_timezone()
    {
        await using var context = CreateContext();
        context.Facilities.Add(Facility("GUAM", "Pacific/Guam", deleted: true));
        await context.SaveChangesAsync();

        var directory = new TenantFacilityDirectory(new EntityRepository<Facility, TenantDbContext>(context));

        var timeZone = await directory.GetTimeZoneAsync("GUAM");

        timeZone.Should().BeNull();
    }
}
