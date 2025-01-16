using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Caching
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;

        public RedisCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public T Get<T>(string key)
        {

            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string value = _cache.GetString(key);

            if (string.IsNullOrEmpty(value)) return default(T);

            try
            {
                return JsonSerializer.Deserialize<T>(value);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to deserialize cached value for key '{key}'", ex);
            }

        }

        public void Set<T>(string key, T value, TimeSpan expiration)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                var options = new DistributedCacheEntryOptions
                {
                    SlidingExpiration = expiration
                };
                _cache.SetString(key, serializedValue, options);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to serialize value for key '{key}'", ex);
            }

        }

        public void Remove(string key)
        {
            _cache.Remove(key);
        }
    }
}
