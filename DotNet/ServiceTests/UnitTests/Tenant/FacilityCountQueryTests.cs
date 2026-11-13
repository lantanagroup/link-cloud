using FluentAssertions;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant;

[Trait("Category", "UnitTests")]
public class FacilityCountQueryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public FacilityCountQueryTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Total_excludes_deleted_and_matched_is_only_set_when_ids_are_supplied()
    {
        await using var context = CreateContext();
        context.Facilities.AddRange(
            Facility("f1", isTest: true),
            Facility("f2"),
            Facility("f3"),
            Facility("f4", deleted: true, isTest: true));
        await context.SaveChangesAsync();

        var open = await FacilityCountQuery.ExecuteAsync(context.Facilities.AsNoTracking(), null, CancellationToken.None);
        open.Total.Should().Be(3);
        open.Matched.Should().BeNull();
        open.Test.Should().Be(1);

        var matched = await FacilityCountQuery.ExecuteAsync(
            context.Facilities.AsNoTracking(),
            ["f1", "f4", "missing"],
            CancellationToken.None);
        matched.Total.Should().Be(3);
        matched.Matched.Should().Be(1);

        var none = await FacilityCountQuery.ExecuteAsync(context.Facilities.AsNoTracking(), [], CancellationToken.None);
        none.Total.Should().Be(3);
        none.Matched.Should().Be(0);
    }

    [Fact]
    public async Task Thousands_of_facilities_return_a_count_without_an_id_list()
    {
        await using var context = CreateContext();
        context.Facilities.AddRange(Enumerable.Range(0, 2000).Select(index => Facility($"facility-{index:D4}")));
        await context.SaveChangesAsync();

        var wanted = Enumerable.Range(0, 50).Select(index => $"facility-{index:D4}").ToList();
        var counts = await FacilityCountQuery.ExecuteAsync(context.Facilities.AsNoTracking(), wanted, CancellationToken.None);

        counts.Total.Should().Be(2000);
        counts.Matched.Should().Be(50);
        counts.Test.Should().Be(0);
    }

    private TenantDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>().UseSqlite(_connection).Options;
        var context = new TenantDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static Facility Facility(string id, bool deleted = false, bool isTest = false) => new()
    {
        FacilityId = id,
        FacilityName = $"Facility {id}",
        TimeZone = "America/New_York",
        IsDeleted = deleted,
        IsTest = isTest,
        ScheduledReports = new ScheduledReportModel { Daily = [], Weekly = [], Monthly = [] }
    };
}
