using Microsoft.Extensions.Caching.Memory;

namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    public interface ICacheService
    {
        T Get<T>(string key);
        void Set<T>(string key, T value, TimeSpan expiration);
        void Remove(string key);
    }
}
