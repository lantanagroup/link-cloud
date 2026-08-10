using LantanaGroup.Link.Shared.Application.Services.DistributedLock;
using Moq;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace UnitTests.Shared.ResourceCache;

[Trait("Category", "UnitTests")]
public class PooledRedisDistributedSemaphoreProviderTests
{
    [Fact]
    public void CreateSemaphore_UsesAPooledDatabaseForEachSemaphore()
    {
        var redisDatabase = new Mock<IRedisDatabase>();
        var database = new Mock<IDatabase>();
        redisDatabase.SetupGet(item => item.Database).Returns(database.Object);
        var provider = new PooledRedisDistributedSemaphoreProvider(redisDatabase.Object);

        provider.CreateSemaphore("first", 1);
        provider.CreateSemaphore("second", 1);

        redisDatabase.VerifyGet(item => item.Database, Times.Exactly(2));
    }
}