using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.ResourceCache;

[Trait("Category", "UnitTests")]
public class RedisResourceCacheTests
{
    [Fact]
    public async Task DeleteAsync_UsesAPooledDatabaseForEachOperation()
    {
        var redisDatabase = new Mock<IRedisDatabase>();
        var database = new Mock<IDatabase>();
        database
            .Setup(item => item.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        redisDatabase.SetupGet(item => item.Database).Returns(database.Object);

        var cache = new RedisResourceCache(
            redisDatabase.Object,
            Options.Create(new ResourceCacheSettings()),
            Mock.Of<ILogger<RedisResourceCache>>());

        await cache.DeleteAsync(new List<string> { "first" });
        await cache.DeleteAsync(new List<string> { "second" });

        redisDatabase.VerifyGet(item => item.Database, Times.Exactly(2));
    }

    [Fact]
    public async Task ReplaceResourcesAsync_ClearsAndRepopulatesInOneTransaction()
    {
        // Atomicity is the point. Done as a delete and then a write, the key is observably empty in
        // between and a reader landing there falls through to durable storage for a key that is about
        // to be perfectly good -- and a tolerated delete failure leaves the old content for the write
        // to merge back into.
        var redisDatabase = new Mock<IRedisDatabase>();
        var database = new Mock<IDatabase>();
        var transaction = new Mock<ITransaction>();

        transaction.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>())).ReturnsAsync(true);
        database.Setup(d => d.CreateTransaction(It.IsAny<object>())).Returns(transaction.Object);
        redisDatabase.SetupGet(item => item.Database).Returns(database.Object);

        var cache = new RedisResourceCache(
            redisDatabase.Object,
            Options.Create(new ResourceCacheSettings()),
            Mock.Of<ILogger<RedisResourceCache>>());

        await cache.ReplaceResourcesAsync(
            "corr-1", [new Patient { Id = "1" }], ResourceType.Patient);

        // Both halves are queued on the transaction, never issued against the database directly.
        transaction.Verify(t => t.KeyDeleteAsync("corr-1", It.IsAny<CommandFlags>()), Times.Once);
        transaction.Verify(t => t.HashSetAsync("corr-1", It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()), Times.Once);
        transaction.Verify(t => t.ExecuteAsync(It.IsAny<CommandFlags>()), Times.Once);
        database.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
        database.Verify(d => d.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceResourcesAsync_WithNoResources_RemovesTheKey()
    {
        var redisDatabase = new Mock<IRedisDatabase>();
        var database = new Mock<IDatabase>();
        database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        redisDatabase.SetupGet(item => item.Database).Returns(database.Object);

        var cache = new RedisResourceCache(
            redisDatabase.Object,
            Options.Create(new ResourceCacheSettings()),
            Mock.Of<ILogger<RedisResourceCache>>());

        // Replacing with nothing is a removal; a transaction that writes no fields would leave the
        // key behind empty.
        await cache.ReplaceResourcesAsync("corr-1", [], ResourceType.Patient);

        database.Verify(d => d.KeyDeleteAsync("corr-1", It.IsAny<CommandFlags>()), Times.Once);
        database.Verify(d => d.CreateTransaction(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_SkipsMetadataFieldsWithoutLoggingAnError()
    {
        // The durable resource count shares the entry's hash with its resources. Deserializing it as
        // FHIR fails, and the catch logs an error -- once per read, on every entry that has had a
        // durable write, which is all of them.
        var redisDatabase = new Mock<IRedisDatabase>();
        var database = new Mock<IDatabase>();
        database
            .Setup(item => item.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(
            [
                new HashEntry("Patient/1", "{\"resourceType\":\"Patient\",\"id\":\"1\"}"),
                new HashEntry("__durableResourceCount", "753")
            ]);
        redisDatabase.SetupGet(item => item.Database).Returns(database.Object);

        var logger = new Mock<ILogger<RedisResourceCache>>();

        var cache = new RedisResourceCache(
            redisDatabase.Object,
            Options.Create(new ResourceCacheSettings()),
            logger.Object);

        var resources = await cache.GetAsync("corr-1");

        Assert.Single(resources);
        logger.Verify(
            item => item.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task AppendResourcesAsync_SetsConfiguredExpiryAfterWritingEntries()
    {
        const int cacheEntryTtlDays = 14;
        var redisDatabase = new Mock<IRedisDatabase>();
        var database = new Mock<IDatabase>();
        database
            .Setup(item => item.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<HashEntry[]>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);
        database
            .Setup(item => item.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        redisDatabase.SetupGet(item => item.Database).Returns(database.Object);

        var cache = new RedisResourceCache(
            redisDatabase.Object,
            Options.Create(new ResourceCacheSettings
            {
                Redis = new ResourceCacheRedisSettings { CacheEntryTtlDays = cacheEntryTtlDays }
            }),
            Mock.Of<ILogger<RedisResourceCache>>());

        await cache.AppendResourcesAsync("correlation-id", [], ResourceType.Patient);

        database.Verify(item => item.HashSetAsync(
            "correlation-id",
            It.IsAny<HashEntry[]>(),
            CommandFlags.None),
            Times.Once);
        database.Verify(item => item.KeyExpireAsync(
            "correlation-id",
            TimeSpan.FromDays(cacheEntryTtlDays),
            ExpireWhen.Always,
            CommandFlags.None),
            Times.Once);
    }
}