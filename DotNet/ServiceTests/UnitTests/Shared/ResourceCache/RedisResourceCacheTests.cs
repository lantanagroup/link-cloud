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
    public async Task UpdateCorrelationCacheAsync_SetsConfiguredExpiryAfterWritingEntries()
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

        await cache.UpdateCorrelationCacheAsync("correlation-id", [], ResourceType.Patient);

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