using Medallion.Threading;
using Medallion.Threading.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace LantanaGroup.Link.Shared.Application.Services.DistributedLock;

public sealed class PooledRedisDistributedSemaphoreProvider : IDistributedSemaphoreProvider
{
    private readonly IRedisDatabase _redisDatabase;

    public PooledRedisDistributedSemaphoreProvider(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase ?? throw new ArgumentNullException(nameof(redisDatabase));
    }

    public IDistributedSemaphore CreateSemaphore(string name, int maxCount)
    {
        var provider = new RedisDistributedSynchronizationProvider(_redisDatabase.Database);
        return provider.CreateSemaphore(name, maxCount);
    }
}