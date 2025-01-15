using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Caching
{
    public class InMemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;

        public InMemoryCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public T Get<T>(string key)
        {
            _cache.TryGetValue(key, out T value);
            return value;
        }

        public void Set<T>(string key, T value)
        {
            _cache.Set(key, value);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
        }
    }
}
