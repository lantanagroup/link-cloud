using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    public class RedisResourceCache : IResourceCache
    {
        private readonly IRedisDatabase _redisDatabase;
        private readonly ILogger<RedisResourceCache> _logger;
        private readonly TimeSpan _cacheEntryTtl;

        public RedisResourceCache(
            IRedisDatabase redisDatabase,
            IOptions<ResourceCacheSettings> settings,
            ILogger<RedisResourceCache> logger)
        {
            _redisDatabase = redisDatabase;
            _logger = logger;

            var cacheEntryTtlDays = settings.Value.Redis.CacheEntryTtlDays;
            if (cacheEntryTtlDays <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    "ResourceCache:Redis:CacheEntryTtlDays must be greater than zero.");
            }

            _cacheEntryTtl = TimeSpan.FromDays(cacheEntryTtlDays);
        }

        public async Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            var database = _redisDatabase.Database;
            await Task.WhenAll(cacheKeys.Select(cacheKey => database.KeyDeleteAsync(cacheKey))).WaitAsync(cancellationToken);
        }

        public async Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var hashEntries = await _redisDatabase.Database.HashGetAllAsync(cacheKey).WaitAsync(cancellationToken);

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

        /// <summary>
        /// Field-name prefix reserved for metadata describing the entry rather than a resource in it.
        /// </summary>
        /// <remarks>
        /// Shared with MeasureEval, which skips these fields when it reads an entry
        /// (<c>RedisResourceService.METADATA_FIELD_PREFIX</c>). Resource fields are always
        /// <c>resourceType/resourceId</c>, so the two cannot collide. Metadata lives in the entry's own
        /// hash so that one lifetime covers both and deleting the entry clears its metadata with it.
        /// See docs-dev/resource-cache.md.
        /// </remarks>
        private const string MetadataFieldPrefix = "__";

        /// <summary>
        /// Hash field holding the count durable storage is known to hold for the entry.
        /// </summary>
        private const string DurableResourceCountField = MetadataFieldPrefix + "durableResourceCount";

        public async Task UpdateCorrelationCacheAsync(string correlationId, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default)
        {
            List<HashEntry> correlationHash = new List<HashEntry>();

            foreach (var resource in resources)
            {
                correlationHash.Add(new HashEntry(resource.TypeName + "/" + resource.Id, resource.ToJson()));
            }

            await _redisDatabase.Database.HashSetAsync(correlationId, correlationHash.ToArray()).WaitAsync(cancellationToken);
            await _redisDatabase.Database.KeyExpireAsync(correlationId, _cacheEntryTtl).WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Completes immediately: this cache has no separate durable tier to wait for.
        /// </summary>
        public Task WaitForDurableAsync(string correlationId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async Task<bool> HasResourcesAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var length = await _redisDatabase.Database.HashLengthAsync(cacheKey).WaitAsync(cancellationToken);
            return length > 0;
        }

        /// <inheritdoc/>
        public async Task<int> GetResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var length = await _redisDatabase.Database.HashLengthAsync(cacheKey).WaitAsync(cancellationToken);

            if (length == 0)
            {
                return 0;
            }

            // The durable-count field shares the hash with the resources, so it must not be counted as
            // one. It cannot collide with a resource field, which is always "<type>/<id>".
            var hasCount = await _redisDatabase.Database
                .HashExistsAsync(cacheKey, DurableResourceCountField)
                .WaitAsync(cancellationToken);

            return (int)(hasCount ? length - 1 : length);
        }

        /// <inheritdoc/>
        public async Task<int?> GetDurableResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var value = await _redisDatabase.Database
                .HashGetAsync(cacheKey, DurableResourceCountField)
                .WaitAsync(cancellationToken);

            return value.HasValue && int.TryParse(value.ToString(), out var count) ? count : null;
        }

        /// <inheritdoc/>
        public async Task SetDurableResourceCountAsync(string cacheKey, int count, CancellationToken cancellationToken = default)
        {
            await _redisDatabase.Database
                .HashSetAsync(cacheKey, DurableResourceCountField, count)
                .WaitAsync(cancellationToken);

            // The field is written after the entry, so refresh the lifetime with it rather than leaving
            // the entry expiring on the clock of its last resource write.
            await _redisDatabase.Database.KeyExpireAsync(cacheKey, _cacheEntryTtl).WaitAsync(cancellationToken);
        }
    }
}
