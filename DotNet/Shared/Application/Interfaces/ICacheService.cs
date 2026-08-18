using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Caching.Memory;

namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    public interface ICacheService
    {
        Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, TimeSpan expiration, ExpirationType expirationType = ExpirationType.Sliding, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    }
}
