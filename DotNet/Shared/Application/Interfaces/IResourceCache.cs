using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    public interface IResourceCache
    {
        Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default);
        Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default);
        Task UpdateCorrelationCacheAsync(string correlationId, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default);
        ResourceType GetResourceTypeByCacheKey(string cacheKey);
        ResourceCacheType GetCacheTypeForCorrelationId(string correlationId);
        Task<ResourceCacheType> GetCacheTypeForCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
        IResourceCache GetImplementation(ResourceCacheType cacheType);

        /// <summary>
        /// True when the backing store has at least one resource for <paramref name="cacheKey"/>,
        /// without deserializing FHIR payloads.
        /// </summary>
        Task<bool> HasResourcesAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Drops any in-process Redis-vs-ABS memo for <paramref name="correlationId"/>.
        /// No-op for implementations that do not memoize a per-correlation cache type.
        /// Shared Redis memos are left in place so other processes can still resolve the type.
        /// </summary>
        void ForgetCacheTypeForCorrelationId(string correlationId);
    }
}
