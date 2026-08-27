using FluentAssertions;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.ResourceCache;

/// <summary>
/// Covers <see cref="HybridResourceCache"/>'s Redis-vs-ABS selection, which since LEGLINK-770
/// derives the max-memory denominator from configuration (<see cref="ResourceCacheRedisSettings.MaxMemoryBytes"/>)
/// rather than Redis <c>INFO maxmemory</c> (Azure Managed Redis does not return it).
/// </summary>
[Trait("Category", "UnitTests")]
public class HybridResourceCacheTests
{
    private readonly Mock<IResourceCache> _redisCache = new();
    private readonly Mock<IResourceCache> _absCache = new();
    private readonly Mock<IRedisDatabase> _redisDatabase = new();
    private readonly Mock<IDatabase> _database = new();
    private readonly Mock<IConnectionMultiplexer> _multiplexer = new();
    private readonly Mock<ILogger<HybridResourceCache>> _logger = new();

    public HybridResourceCacheTests()
    {
        _redisDatabase.SetupGet(database => database.Database).Returns(_database.Object);
        _database.SetupGet(database => database.Multiplexer).Returns(_multiplexer.Object);
    }

    private HybridResourceCache CreateSut(ResourceCacheRedisSettings redisSettings)
    {
        var settings = new ResourceCacheSettings { Redis = redisSettings };
        return new HybridResourceCache(
            _redisCache.Object,
            _absCache.Object,
            _redisDatabase.Object,
            Options.Create(settings),
            _logger.Object);
    }

    /// <summary>Configures a connected Redis server whose INFO memory section returns the given used_memory.</summary>
    private void SetupRedisUsedMemory(long usedMemory)
    {
        SetupRedisInfo(new[] { new KeyValuePair<string, string>("used_memory", usedMemory.ToString()) });
    }

    private void SetupRedisInfo(IEnumerable<KeyValuePair<string, string>> memoryPairs)
    {
        var grouping = memoryPairs.GroupBy(_ => "memory").First();
        var server = new Mock<IServer>();
        server.SetupGet(s => s.IsConnected).Returns(true);
        server.Setup(s => s.InfoAsync(It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new[] { grouping });
        _multiplexer.Setup(m => m.GetServers()).Returns(new[] { server.Object });
    }

    private void SetupNoConnectedServer()
    {
        _multiplexer.Setup(m => m.GetServers()).Returns(Array.Empty<IServer>());
    }

    /// <summary>Configures a connected Redis server whose INFO memory section omits used_memory.</summary>
    private Mock<IServer> SetupServerWithoutUsedMemory()
    {
        var server = new Mock<IServer>();
        server.SetupGet(s => s.IsConnected).Returns(true);
        server.Setup(s => s.InfoAsync(It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new[] { new[] { new KeyValuePair<string, string>("some_other_stat", "123") }.GroupBy(_ => "memory").First() });
        _multiplexer.Setup(m => m.GetServers()).Returns(new[] { server.Object });
        return server;
    }

    private static void SetupMemoryStats(Mock<IServer> server, long totalAllocated)
    {
        server.Setup(s => s.MemoryStatsAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(new RedisValue[] { "total.allocated", totalAllocated.ToString() }));
    }

    private Task Write(HybridResourceCache sut, string correlationId)
    {
        return sut.UpdateCorrelationCacheAsync(correlationId, new List<DomainResource>(), ResourceType.Patient);
    }

    private void VerifyWroteTo(Mock<IResourceCache> expected, Mock<IResourceCache> notExpected)
    {
        expected.Verify(c => c.UpdateCorrelationCacheAsync(It.IsAny<string>(), It.IsAny<List<DomainResource>>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()), Times.Once);
        notExpected.Verify(c => c.UpdateCorrelationCacheAsync(It.IsAny<string>(), It.IsAny<List<DomainResource>>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifyWarningLogged(Times times)
    {
        VerifyLogged(LogLevel.Warning, times);
    }

    private void VerifyLogged(LogLevel level, Times times)
    {
        _logger.Verify(
            log => log.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    [Fact]
    public async Task UsedMemory_below_threshold_of_configured_max_uses_Redis()
    {
        // 100 MB used of 1000 MB max = 10%, threshold 80% => Redis
        SetupRedisUsedMemory(100L * 1024 * 1024);
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-below");

        VerifyWroteTo(_redisCache, _absCache);
    }

    [Fact]
    public async Task UsedMemory_at_or_above_threshold_of_configured_max_uses_ABS()
    {
        // 900 MB used of 1000 MB max = 90%, threshold 80% => ABS
        SetupRedisUsedMemory(900L * 1024 * 1024);
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-above");

        VerifyWroteTo(_absCache, _redisCache);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task MaxMemoryBytes_missing_or_invalid_uses_Redis_and_logs_warning(long? maxMemoryBytes)
    {
        SetupRedisUsedMemory(900L * 1024 * 1024); // would be ABS if a valid max were configured
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = maxMemoryBytes, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-nomax");

        VerifyWroteTo(_redisCache, _absCache);
        VerifyWarningLogged(Times.Once());
    }

    /// <summary>
    /// Azure Managed Redis proxies Redis Enterprise and may not expose used_memory via INFO memory,
    /// the same restriction that hides maxmemory and drove LEGLINK-770. Deciding the cache on the
    /// first miss would be deciding on no evidence, so MEMORY STATS is consulted before giving up.
    /// </summary>
    [Fact]
    public async Task UsedMemory_missing_from_info_falls_back_to_MEMORY_STATS()
    {
        var server = SetupServerWithoutUsedMemory();
        SetupMemoryStats(server, 100L * 1024 * 1024); // 100 MB of 1000 MB = 10%, under the 80% threshold
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-memorystats-under");

        VerifyWroteTo(_redisCache, _absCache);
    }

    [Fact]
    public async Task MEMORY_STATS_total_allocated_is_measured_against_the_configured_max()
    {
        var server = SetupServerWithoutUsedMemory();
        SetupMemoryStats(server, 900L * 1024 * 1024); // 900 MB of 1000 MB = 90%, over the 80% threshold
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-memorystats-over");

        VerifyWroteTo(_absCache, _redisCache);
    }

    /// <summary>
    /// When INFO memory, MEMORY STATS and a full INFO dump all fail to yield a used-memory figure
    /// there is no numerator to measure against the configured limit, so the pressure-safe choice
    /// is ABS.
    /// </summary>
    [Fact]
    public async Task UsedMemory_unavailable_from_every_source_uses_ABS()
    {
        SetupServerWithoutUsedMemory(); // MEMORY STATS left unconfigured, so it yields nothing
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-nousedmem");

        VerifyWroteTo(_absCache, _redisCache);
    }

    [Fact]
    public async Task DeleteAsync_does_not_forget_cache_type_for_remaining_keys_of_the_same_correlation()
    {
        SetupRedisUsedMemory(900L * 1024 * 1024);
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await sut.UpdateCorrelationCacheAsync("corr-1:Patient", new List<DomainResource>(), ResourceType.Patient);

        _absCache
            .Setup(c => c.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _absCache
            .Setup(c => c.GetAsync("corr-1:Patient", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DomainResource>());

        await sut.DeleteAsync(new List<string> { "corr-1:Encounter" });

        sut.GetCacheTypeForCorrelationId("corr-1").Should().Be(ResourceCacheType.ABS);

        await sut.GetAsync("corr-1:Patient");

        _absCache.Verify(c => c.GetAsync("corr-1:Patient", It.IsAny<CancellationToken>()), Times.Once);
        _redisCache.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_connected_server_uses_ABS()
    {
        SetupNoConnectedServer();
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-noserver");

        VerifyWroteTo(_absCache, _redisCache);
    }

    [Fact]
    public async Task Exception_reading_memory_uses_ABS()
    {
        _multiplexer.Setup(m => m.GetServers()).Throws(new RedisException("boom"));
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-throws");

        VerifyWroteTo(_absCache, _redisCache);
    }

    /// <summary>
    /// A rejected INFO means the command is restricted, not that Redis is under pressure. It must
    /// not short-circuit the probe before MEMORY STATS has been consulted.
    /// </summary>
    [Fact]
    public async Task Rejected_INFO_still_falls_back_to_MEMORY_STATS()
    {
        var server = new Mock<IServer>();
        server.SetupGet(s => s.IsConnected).Returns(true);
        server.Setup(s => s.InfoAsync(It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisCommandException("This operation is not available unless admin mode is enabled: INFO"));
        SetupMemoryStats(server, 100L * 1024 * 1024);
        _multiplexer.Setup(m => m.GetServers()).Returns(new[] { server.Object });
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-info-rejected");

        VerifyWroteTo(_redisCache, _absCache);
    }

    /// <summary>
    /// The selection decision must be visible in deployed environments, which run at Information.
    /// Logging it at Debug left LEGLINK-948 undiagnosable: an ABS result could not be distinguished
    /// from a failed probe without redeploying at a different log level.
    /// </summary>
    [Fact]
    public async Task Selection_decision_is_logged_at_Information()
    {
        SetupRedisUsedMemory(100L * 1024 * 1024);
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-logged");

        VerifyLogged(LogLevel.Information, Times.AtLeastOnce());
    }

    [Fact]
    public async Task No_connected_server_logs_a_warning()
    {
        SetupNoConnectedServer();
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-noserver-warn");

        VerifyWarningLogged(Times.Once());
    }

    /// <summary>
    /// Azure Managed Redis proxies Redis Enterprise and is not guaranteed to return the unique-key
    /// INFO shape that open-source Redis does. A duplicate key previously threw out of
    /// <c>ToDictionary</c> and was swallowed by the catch-all into a silent ABS fallback.
    /// </summary>
    [Fact]
    public async Task Duplicate_keys_in_info_memory_do_not_force_the_ABS_fallback()
    {
        var usedMemory = (100L * 1024 * 1024).ToString();
        SetupRedisInfo(new[]
        {
            new KeyValuePair<string, string>("used_memory", usedMemory),
            new KeyValuePair<string, string>("used_memory", usedMemory)
        });
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-duplicate-keys");

        VerifyWroteTo(_redisCache, _absCache);
    }

    [Fact]
    public async Task Decision_is_memoized_per_correlationId()
    {
        SetupRedisUsedMemory(100L * 1024 * 1024);
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-memo");
        await Write(sut, "corr-memo");

        // The memory pressure check (GetServers) should only happen once for the same correlationId.
        _multiplexer.Verify(m => m.GetServers(), Times.Once);
        _redisCache.Verify(c => c.UpdateCorrelationCacheAsync(It.IsAny<string>(), It.IsAny<List<DomainResource>>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
