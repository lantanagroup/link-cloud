using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class HSLOCManagerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public HSLOCManagerTests()
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

    private static HSLOCManager CreateManager(NormalizationDbContext context) =>
        new(context, new Mock<ILogger<HSLOCManager>>().Object);

    private static MemoryStream CreateCsv(params string[] rows)
    {
        var csv = string.Join(Environment.NewLine,
            new[] { "CDCCode,ShortDescription,HSLOCCode,LongDescription" }.Concat(rows));
        return new MemoryStream(Encoding.UTF8.GetBytes(csv));
    }

    [Fact]
    public async Task Update_ValidCsv_UpdatesMatchingRowsAddsNewRowsAndDeactivatesRemovedRows()
    {
        using var context = CreateContext();
        var matching = new HSLOC
        {
            CDCCode = "old-cdc",
            ShortDescription = "old short",
            HSLOCCode = "A1",
            LongDescription = "old long",
            Version = "2025",
            IsActive = false
        };
        var removed = new HSLOC
        {
            CDCCode = "removed-cdc",
            ShortDescription = "removed short",
            HSLOCCode = "B2",
            LongDescription = "removed long",
            Version = "2025",
            IsActive = true
        };
        var unrelated = new HSLOC
        {
            CDCCode = "unrelated-cdc",
            ShortDescription = "unrelated short",
            HSLOCCode = "C3",
            LongDescription = "unrelated long",
            Version = "2024",
            IsActive = true
        };
        context.HSLOCS.AddRange(matching, removed, unrelated);
        await context.SaveChangesAsync();

        await using var csv = CreateCsv(
            "updated-cdc,updated short,A1,updated long",
            "new-cdc,new short,D4,new long");

        await CreateManager(context).Update("2025", "2026", csv);

        var stored = await context.HSLOCS.OrderBy(row => row.HSLOCCode).ToListAsync();
        Assert.Equal(4, stored.Count);

        var updated = Assert.Single(stored, row => row.HSLOCCode == "A1");
        Assert.Equal("updated-cdc", updated.CDCCode);
        Assert.Equal("updated short", updated.ShortDescription);
        Assert.Equal("updated long", updated.LongDescription);
        Assert.Equal("2026", updated.Version);
        Assert.True(updated.IsActive);

        var added = Assert.Single(stored, row => row.HSLOCCode == "D4");
        Assert.Equal("2026", added.Version);
        Assert.True(added.IsActive);

        Assert.False(Assert.Single(stored, row => row.HSLOCCode == "B2").IsActive);
        Assert.True(Assert.Single(stored, row => row.HSLOCCode == "C3").IsActive);
    }

    [Fact]
    public async Task Update_DuplicateImportedCode_ThrowsArgumentExceptionWithoutWritingChanges()
    {
        using var context = CreateContext();
        context.HSLOCS.Add(new HSLOC
        {
            CDCCode = "existing-cdc",
            ShortDescription = "existing short",
            HSLOCCode = "A1",
            LongDescription = "existing long",
            Version = "2025"
        });
        await context.SaveChangesAsync();
        await using var csv = CreateCsv(
            "first-cdc,first short,A1,first long",
            "second-cdc,second short,a1,second long");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateManager(context).Update("2025", "2026", csv));

        Assert.Contains("duplicate HSLOC code", exception.Message, StringComparison.OrdinalIgnoreCase);
        var stored = Assert.Single(await context.HSLOCS.ToListAsync());
        Assert.Equal("2025", stored.Version);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task DeleteAll_RemovesEveryRecord()
    {
        using var context = CreateContext();
        context.HSLOCS.AddRange(CreateHSLOC("A1", "2025"), CreateHSLOC("B2", "2026"));
        await context.SaveChangesAsync();

        await CreateManager(context).DeleteAll();

        Assert.Empty(await context.HSLOCS.ToListAsync());
    }

    [Fact]
    public async Task DeleteByVersion_RemovesOnlyTheRequestedVersion()
    {
        using var context = CreateContext();
        context.HSLOCS.AddRange(CreateHSLOC("A1", "2025"), CreateHSLOC("B2", "2026"));
        await context.SaveChangesAsync();

        await CreateManager(context).DeleteByVersion("2025");

        var remaining = Assert.Single(await context.HSLOCS.ToListAsync());
        Assert.Equal("2026", remaining.Version);
    }

    [Fact]
    public async Task DeleteById_RemovesOnlyTheRequestedRecord()
    {
        using var context = CreateContext();
        var remove = CreateHSLOC("A1", "2025");
        var retain = CreateHSLOC("B2", "2025");
        context.HSLOCS.AddRange(remove, retain);
        await context.SaveChangesAsync();

        await CreateManager(context).DeleteById(remove.Id);

        var remaining = Assert.Single(await context.HSLOCS.ToListAsync());
        Assert.Equal(retain.Id, remaining.Id);
    }

    private static HSLOC CreateHSLOC(string hslocCode, string version) => new()
    {
        CDCCode = $"cdc-{hslocCode}",
        ShortDescription = $"short-{hslocCode}",
        HSLOCCode = hslocCode,
        LongDescription = $"long-{hslocCode}",
        Version = version
    };
}