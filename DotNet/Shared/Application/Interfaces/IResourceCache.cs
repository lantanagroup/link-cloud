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
        IResourceCache GetImplementation(ResourceCacheType cacheType);
    }
}
