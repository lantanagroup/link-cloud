using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations.DistributedLock;
public class DistributedSemaphoreRepository : IDistributedSemaphoreRepository
{
    private readonly ILogger<DistributedSemaphoreRepository> _logger;
    private readonly DistributedLockSettings _distributedLockSettings;

    public DistributedSemaphoreRepository(ILogger<DistributedSemaphoreRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> IsLockAcquired(string lockKey, string lockValue, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ReleaseLockAsync(string lockKey, string lockValue, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<(string key, string value)> TryAcquireLockAsync(string lockKey, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
        //await using var redLock = await _redLockFactory.CreateLockAsync(lockKey, expiration);

        //if (redLock.IsAcquired)
        //{
        //    return redLock;
        //}

        //return null;
    }

    public async Task<bool> GetLockUpdateValueAsync(string lockKey, string lockValue, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
        //await using var redLock = await _redLockFactory.CreateLockAsync(lockKey, expiration);
        //if (redLock.IsAcquired)
        //{
        //    _logger.LogInformation("Lock acquired successfully.");

        //    redLock.

        //    return true;
        //}
        //else
        //{
        //    _logger.LogWarning("Failed to acquire lock.");
        //    return false;
        //}
    }
}
