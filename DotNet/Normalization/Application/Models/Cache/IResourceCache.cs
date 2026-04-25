using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Normalization.Application.Models.Cache
{
    public interface IResourceCache
    {
        ResourceType GetResourceTypeByEventKey(string cacheKey);
        List<DomainResource> Get(string cacheKey);
        void UpdateCorrelationCache(string correlationId, List<DomainResource> resources, ResourceType resourceType);
        void CacheSkipped(string sourceCache, string correlationId);
        void Delete(List<string> cacheKeys);
    }
}
