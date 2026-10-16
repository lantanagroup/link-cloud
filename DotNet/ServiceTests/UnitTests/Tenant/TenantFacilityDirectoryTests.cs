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

    [Fact]
    public async Task Facilities_in_a_zone_include_every_active_survivor()
    {
        await using var context = CreateContext();
        context.Facilities.AddRange(
            Facility("1", "America/Chicago"),
            Facility("2", "America/Chicago"),
            Facility("3", "America/Chicago", deleted: true),
            Facility("4", "America/New_York"));
        await context.SaveChangesAsync();

        var directory = new TenantFacilityDirectory(new EntityRepository<Facility, TenantDbContext>(context));

        var facilities = await directory.GetActiveInTimeZoneAsync("America/Chicago");

        facilities.Should().BeEquivalentTo(
        [
            new ScheduledFacility("1", "America/Chicago"),
            new ScheduledFacility("2", "America/Chicago")
        ]);
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

    [Fact]
    public async Task GetTimeZoneAsync_returns_the_facilitys_stored_timezone()
    {
        await using var context = CreateContext();
        context.Facilities.Add(Facility("MAJURO", "Pacific/Majuro"));
        await context.SaveChangesAsync();

        var directory = new TenantFacilityDirectory(new EntityRepository<Facility, TenantDbContext>(context));

        var timeZone = await directory.GetTimeZoneAsync("MAJURO");

        timeZone.Should().Be("Pacific/Majuro");
    }

    [Fact]
    public async Task GetTimeZoneAsync_returns_only_the_requested_facilitys_timezone()
    {
        await using var context = CreateContext();
        context.Facilities.AddRange(
            Facility("MAJURO", "Pacific/Majuro"),
            Facility("PAGO-PAGO", "Pacific/Pago_Pago"));
        await context.SaveChangesAsync();

        var directory = new TenantFacilityDirectory(new EntityRepository<Facility, TenantDbContext>(context));

        var timeZone = await directory.GetTimeZoneAsync("PAGO-PAGO");

        timeZone.Should().Be("Pacific/Pago_Pago");
    }

    /// <summary>
    /// Null is the module's signal that Link has no such facility, which the reads treat as an
    /// ordinary empty answer and log quietly.
    /// </summary>
    [Fact]
    public async Task GetTimeZoneAsync_returns_null_for_an_id_no_facility_has()
    {
        await using var context = CreateContext();
        context.Facilities.Add(Facility("MAJURO", "Pacific/Majuro"));
        await context.SaveChangesAsync();

        var directory = new TenantFacilityDirectory(new EntityRepository<Facility, TenantDbContext>(context));

        var timeZone = await directory.GetTimeZoneAsync("NO-SUCH-FACILITY");

        timeZone.Should().BeNull();
    }

    /// <summary>
    /// A blank timezone on a facility that exists is a data problem the module warns about. Collapsing
    /// it to null would disguise it as a facility that does not exist, and the warning would never fire.
    /// </summary>
    [Fact]
    public async Task GetTimeZoneAsync_returns_a_blank_timezone_as_blank_rather_than_null()
    {
        await using var context = CreateContext();
        context.Facilities.Add(Facility("BLANK", ""));
        await context.SaveChangesAsync();

        var directory = new TenantFacilityDirectory(new EntityRepository<Facility, TenantDbContext>(context));

        var timeZone = await directory.GetTimeZoneAsync("BLANK");

        timeZone.Should().Be(string.Empty);
    }

    [Fact]
    public void Constructor_refuses_a_missing_repository()
    {
        var act = () => new TenantFacilityDirectory(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("facilities");
    }
}
