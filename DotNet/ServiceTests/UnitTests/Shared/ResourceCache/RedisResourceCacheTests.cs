using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.Logging;
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

        var cache = new RedisResourceCache(redisDatabase.Object, Mock.Of<ILogger<RedisResourceCache>>());

        await cache.DeleteAsync(new List<string> { "first" });
        await cache.DeleteAsync(new List<string> { "second" });

        redisDatabase.VerifyGet(item => item.Database, Times.Exactly(2));
    }
}