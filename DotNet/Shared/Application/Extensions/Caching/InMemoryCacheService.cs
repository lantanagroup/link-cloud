using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Caching.Memory;

namespace LantanaGroup.Link.Shared.Application.Extensions.Caching
{
    public class InMemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;

        public InMemoryCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            cancellationToken.ThrowIfCancellationRequested();

            if (_cache.TryGetValue(key, out T value))
                return Task.FromResult(value!);

            return Task.FromResult(default(T)!);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration, ExpirationType expirationType = ExpirationType.Sliding, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            cancellationToken.ThrowIfCancellationRequested();

            var options = expirationType == ExpirationType.Sliding
                ? new MemoryCacheEntryOptions().SetSlidingExpiration(expiration).SetSize(1)
                : new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiration).SetSize(1);

            _cache.Set(key, value, options);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            cancellationToken.ThrowIfCancellationRequested();
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
