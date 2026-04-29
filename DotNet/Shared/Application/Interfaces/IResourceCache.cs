using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    public interface IResourceCache
    {
        List<DomainResource> Get(string cacheKey);
        void Skipped(string sourceCache, string correlationId);
        void Delete(List<string> cacheKeys);
        void UpdateCorrelationCache(string correlationId, List<DomainResource> resources, ResourceType resourceType);
        ResourceType GetResourceTypeByCacheKey(string cacheKey);
    }
}
