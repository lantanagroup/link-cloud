using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class HSLOCQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public HSLOCQueriesTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private NormalizationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseSqlite(_connection)
            .Options;
        var context = new NormalizationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task GetAll_DefaultFilter_ReturnsOnlyActiveRecords()
    {
        using var context = CreateContext();
        context.HSLOCS.AddRange(
            CreateHSLOC("active", isActive: true),
            CreateHSLOC("inactive", isActive: false));
        await context.SaveChangesAsync();

        var result = await new HSLOCQueries(context).GetAll();

        var record = Assert.Single(result);
        Assert.Equal("active", record.HSLOCCode);
    }

    [Fact]
    public async Task GetAll_IncludeInactive_ReturnsActiveAndInactiveRecords()
    {
        using var context = CreateContext();
        context.HSLOCS.AddRange(
            CreateHSLOC("active", isActive: true),
            CreateHSLOC("inactive", isActive: false));
        await context.SaveChangesAsync();

        var result = await new HSLOCQueries(context).GetAll(includeInactive: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, record => record.HSLOCCode == "active");
        Assert.Contains(result, record => record.HSLOCCode == "inactive");
    }

    private static HSLOC CreateHSLOC(string hslocCode, bool isActive) => new()
    {
        CDCCode = $"cdc-{hslocCode}",
        ShortDescription = $"short-{hslocCode}",
        HSLOCCode = hslocCode,
        LongDescription = $"long-{hslocCode}",
        Version = "2026",
        IsActive = isActive
    };
}