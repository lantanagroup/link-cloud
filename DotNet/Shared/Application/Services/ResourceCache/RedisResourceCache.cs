using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    public class RedisResourceCache : IResourceCache
    {
        private readonly IDatabase _db;
        private readonly ILogger<RedisResourceCache> _logger;

        public RedisResourceCache(IConnectionMultiplexer redis, ILogger<RedisResourceCache> logger)
        {
            _db = redis.GetDatabase();
            _logger = logger;
        }

        public async Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(cacheKeys.Select(cacheKey => _db.KeyDeleteAsync(cacheKey))).WaitAsync(cancellationToken);
        }

        public async Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var hashEntries = await _db.HashGetAllAsync(cacheKey).WaitAsync(cancellationToken);

            if (hashEntries == null || hashEntries.Length == 0) {
                return new List<DomainResource>();
            }

            List<DomainResource> resources = new List<DomainResource>();

            foreach (var entry in hashEntries) {
                try
                {
                    DomainResource resource = JsonSerializer.Deserialize<DomainResource>(entry.Value, LinkFhirSerializerOptions.ForFhirLenientSerialization);
                    resources.Add(resource);
                }
                catch (Exception ex) 
                {
                    //We aren't going to dead letter the event if we have issues deserializing the resource, but will log it.
                    _logger.LogError("Failed to deserialize FHIR Domain resource for Redis entry: {entryName}", entry.Name.ToString());
                }
            }

            return resources;
        }

        public ResourceType GetResourceTypeByCacheKey(string cacheKey)
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

        public async Task UpdateCorrelationCacheAsync(string correlationId, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default)
        {
            List<HashEntry> correlationHash = new List<HashEntry>();

            foreach (var resource in resources)
            {
                correlationHash.Add(new HashEntry(resource.TypeName + "/" + resource.Id, resource.ToJson()));
            }

            await _db.HashSetAsync(correlationId, correlationHash.ToArray()).WaitAsync(cancellationToken);
        }

        public ResourceCacheType GetCacheTypeForCorrelationId(string correlationId)
        {
            return ResourceCacheType.Redis;
        }

        public IResourceCache GetImplementation(ResourceCacheType cacheType)
        {
            if (cacheType != ResourceCacheType.Redis)
                throw new NotSupportedException($"{nameof(RedisResourceCache)} does not support cache type '{cacheType}'.");
            return this;
        }
    }
}
