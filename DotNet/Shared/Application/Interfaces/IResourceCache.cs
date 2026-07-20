using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    public interface IResourceCache
    {
        List<DomainResource> Get(string cacheKey);
        void Delete(List<string> cacheKeys);
        void UpdateCorrelationCache(string correlationId, List<DomainResource> resources, ResourceType resourceType);
        ResourceType GetResourceTypeByCacheKey(string cacheKey);
        ResourceCacheType GetCacheTypeForCorrelationId(string correlationId);
        IResourceCache GetImplementation(ResourceCacheType cacheType);
    }
}
