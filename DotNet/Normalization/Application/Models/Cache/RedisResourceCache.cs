using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.SerDes;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Normalization.Application.Models.Cache
{
    public class RedisResourceCache : IResourceCache
    {
        private readonly IDatabase _db;

        public RedisResourceCache(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public void Delete(List<string> cacheKeys)
        {
            foreach (var cacheKey in cacheKeys)
            {
                _db.KeyDelete(cacheKey);
            }
        }

        public List<DomainResource> Get(string cacheKey)
        {
            var hashEntries = _db.HashGetAll(cacheKey);

            if (hashEntries == null || hashEntries.Length == 0) {
                return new List<DomainResource>();
            }

            List<DomainResource> resources = new List<DomainResource>();

            foreach (var entry in hashEntries) {
                //TODO: Daniel - Add Null check or something
                DomainResource resource = JsonSerializer.Deserialize<DomainResource>(entry.Value, LinkFhirSerializerOptions.ForFhirLenientSerialization);
                resources.Add(resource);
            }

            return resources;
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

        public void UpdateCorrelationCache(string correlationId, List<DomainResource> resources, ResourceType resourceType, out string destination)
        {
            List<HashEntry> correlationHash = new List<HashEntry>();

            foreach (var resource in resources)
            {
                correlationHash.Add(new HashEntry(resource.TypeName + "/" + resource.Id, resource.ToJson()));
            }

            _db.HashSet(correlationId, correlationHash.ToArray());

            destination = correlationId;
        }

        public void CopyResourcesToCorrelationCache(string sourceCache, string destinationCache)
        {
            var hashEntries = _db.HashGetAll(sourceCache);

            _db.HashSet(destinationCache, hashEntries);
        }
    }
}
