using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Distributed;
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
            string value = _cache.GetString(key);
            return value!=null ? JsonSerializer.Deserialize<T>(value) : default;
        }

        public void Set<T>(string key, T value)
        {
            var serializedValue = JsonSerializer.Serialize(value);
            _cache.SetString(key, serializedValue);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
        }
    }
}
