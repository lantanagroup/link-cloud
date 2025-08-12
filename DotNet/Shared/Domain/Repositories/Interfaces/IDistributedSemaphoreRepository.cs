namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
public interface IDistributedSemaphoreRepository
{
    Task<(string key, string value)> TryAcquireLockAsync(string lockKey, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task<bool> ReleaseLockAsync(string lockKey, string lockValue, CancellationToken cancellationToken = default);
    Task<bool> GetLockUpdateValueAsync(string lockKey, string lockValue, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task<bool> IsLockAcquired(string lockKey, string lockValue, CancellationToken cancellationToken = default);

}
