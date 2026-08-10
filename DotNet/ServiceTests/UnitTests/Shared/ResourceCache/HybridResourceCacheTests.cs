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
        _logger.Verify(
            log => log.Log(
                LogLevel.Warning,
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

    [Fact]
    public async Task UsedMemory_missing_from_info_uses_Redis()
    {
        SetupRedisInfo(new[] { new KeyValuePair<string, string>("some_other_stat", "123") });
        var sut = CreateSut(new ResourceCacheRedisSettings { MaxMemoryBytes = 1000L * 1024 * 1024, MemoryThresholdPercent = 80.0 });

        await Write(sut, "corr-nousedmem");

        VerifyWroteTo(_redisCache, _absCache);
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
