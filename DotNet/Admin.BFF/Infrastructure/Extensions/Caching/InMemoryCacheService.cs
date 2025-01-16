using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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
            if (string.IsNullOrEmpty(key))  throw new ArgumentNullException(nameof(key));

            if (_cache.TryGetValue(key, out T value))
               return value;

            return default(T);
        }

        public void Set<T>(string key, T value, TimeSpan expiration)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            
            var options = new MemoryCacheEntryOptions().SetSlidingExpiration(expiration).SetSize(1); 
            
            _cache.Set(key, value, options);
        }

        public void Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            _cache.Remove(key);
        }
    }
}
