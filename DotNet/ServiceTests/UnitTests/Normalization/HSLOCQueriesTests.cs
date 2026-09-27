using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class HSLOCQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly HSLOCLookupCache _lookupCache;

    public HSLOCQueriesTests()
    {
        _lookupCache = new HSLOCLookupCache(_memoryCache);
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
        _lookupCache.Dispose();
        _memoryCache.Dispose();
    }

    private NormalizationDbContext CreateContext(DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<NormalizationDbContext>().UseSqlite(_connection);
        if (interceptor != null)
        {
            options.AddInterceptors(interceptor);
        }
        var context = new NormalizationDbContext(options.Options);
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

        var result = await new HSLOCQueries(context, _lookupCache).GetAll();

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

        var result = await new HSLOCQueries(context, _lookupCache).GetAll(includeInactive: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, record => record.HSLOCCode == "active");
        Assert.Contains(result, record => record.HSLOCCode == "inactive");
    }

    [Fact]
    public async Task GetActiveLookup_ReusesSnapshotAcrossContextsUntilInvalidated()
    {
        using var context = CreateContext();
        var active = CreateHSLOC("active", true);
        context.HSLOCS.AddRange(active, CreateHSLOC("inactive", false));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var first = await new HSLOCQueries(context, _lookupCache).GetActiveLookup();
        Assert.Equal(active.Id, Assert.Single(first).Value);
        Assert.False(first.ContainsKey("ACTIVE"));
        Assert.Empty(context.ChangeTracker.Entries());

        await context.HSLOCS.ExecuteDeleteAsync();
        using var otherContext = CreateContext();
        var queries = new HSLOCQueries(otherContext, _lookupCache);
        Assert.Same(first, await queries.GetActiveLookup());

        _lookupCache.Invalidate();
        Assert.Empty(await queries.GetActiveLookup());
    }

    [Fact]
    public async Task GetActiveLookup_CanceledCallerThrowsEvenWhenCached()
    {
        using var context = CreateContext();
        var queries = new HSLOCQueries(context, _lookupCache);
        await queries.GetActiveLookup();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queries.GetActiveLookup(cancellation.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetActiveLookup_ConcurrentMisses_OnlyReloadsWhenInvalidated(bool invalidateDuringLoad)
    {
        var interceptor = new BlockingReaderInterceptor();
        using var firstContext = CreateContext(interceptor);
        using var secondContext = CreateContext(interceptor);
        firstContext.HSLOCS.Add(CreateHSLOC("active", true));
        await firstContext.SaveChangesAsync();
        interceptor.Enabled = true;

        var firstLoad = new HSLOCQueries(firstContext, _lookupCache).GetActiveLookup();
        await interceptor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (invalidateDuringLoad)
        {
            _lookupCache.Invalidate();
        }
        var secondLoad = new HSLOCQueries(secondContext, _lookupCache).GetActiveLookup();
        interceptor.Release.TrySetResult();
        var results = await Task.WhenAll(firstLoad, secondLoad).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(invalidateDuringLoad ? 2 : 1, interceptor.ReadCount);
        Assert.Single(results[0]);
        Assert.Single(results[1]);
        if (!invalidateDuringLoad)
        {
            Assert.Same(results[0], results[1]);
        }
    }

    [Fact]
    public async Task GetActiveLookup_ExpiresAfterOneMinuteDespiteCacheHits()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new Moq.Mock<Microsoft.Extensions.Internal.ISystemClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => now);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { Clock = clock.Object });
        using var lookupCache = new HSLOCLookupCache(memoryCache);
        using var context = CreateContext();
        context.HSLOCS.Add(CreateHSLOC("active", true));
        await context.SaveChangesAsync();
        var queries = new HSLOCQueries(context, lookupCache);
        var first = await queries.GetActiveLookup();
        Assert.Single(first);
        await context.HSLOCS.ExecuteDeleteAsync();

        now = now.AddSeconds(40);
        Assert.Same(first, await queries.GetActiveLookup());
        now = now.AddSeconds(30);
        Assert.Empty(await queries.GetActiveLookup());
    }

    private sealed class BlockingReaderInterceptor : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public int ReadCount { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                ReadCount++;
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
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