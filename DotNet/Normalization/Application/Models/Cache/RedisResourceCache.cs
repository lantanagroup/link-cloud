using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Normalization.Application.Models.Cache
{
    public class RedisResourceCache : IResourceCache
    {
        public List<DomainResource> GetResourcesByType(ResourceType type)
        {
            throw new NotImplementedException();
        }

        public ResourceType GetResourceTypeByEventKey(string cacheKey)
        {
            string[] splitKey = cacheKey.Split(":");
            
            if (splitKey.Length != 2) 
            {
                throw new Exception($"Cache key '{cacheKey}' does not contain required ':' divider. Expected format is <correlation id>:<resource type>");
            }

            if (Enum.TryParse<ResourceType>(splitKey[1], out var resourceType))
            {
                return resourceType;
            }
            else
            {
                throw new Exception($"Could not parse the Redis cache key '{cacheKey}' into a valid FHIR Resource Type");
            }
        }

        public void UpdateResourcesByType(List<DomainResource> resources)
        {
            throw new NotImplementedException();
        }
    }
}
