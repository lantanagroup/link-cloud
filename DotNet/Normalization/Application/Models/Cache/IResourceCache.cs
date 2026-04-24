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
        void UpdateResourcesByType(List<DomainResource> resources);
    }
}
