using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    /// <summary>
    /// Selects Redis or ABS per correlationId based on Redis memory usage at the time of the
    /// first write. The decision is recorded in a dictionary so that all subsequent operations
    /// for the same correlationId use the same implementation, and so that message-builders can
    /// embed the correct <see cref="ResourceCacheType"/> in the Kafka ResourcesAcquired event.
    /// </summary>
    public class HybridResourceCache : IResourceCache
    {
        private readonly IResourceCache _redisCache;
        private readonly IResourceCache _absCache;
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly ResourceCacheSettings _settings;
        private readonly ILogger<HybridResourceCache> _logger;

        private readonly ConcurrentDictionary<string, ResourceCacheType> _correlationCacheTypes = new();

        public HybridResourceCache(
            [FromKeyedServices(ResourceCacheType.Redis)] IResourceCache redisCache,
            [FromKeyedServices(ResourceCacheType.ABS)] IResourceCache absCache,
            IConnectionMultiplexer multiplexer,
            IOptions<ResourceCacheSettings> settings,
            ILogger<HybridResourceCache> logger)
        {
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
            _absCache = absCache ?? throw new ArgumentNullException(nameof(absCache));
            _multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public void UpdateCorrelationCache(string correlationId, List<DomainResource> resources, ResourceType resourceType)
        {
            var cache = DetermineAndRecordCache(correlationId);
            cache.UpdateCorrelationCache(correlationId, resources, resourceType);
        }

        /// <inheritdoc/>
        public List<DomainResource> Get(string cacheKey)
        {
            var cache = ResolveFromKey(cacheKey);
            return cache.Get(cacheKey);
        }

        /// <inheritdoc/>
        public void Skipped(string sourceCache, string correlationId)
        {
            var cache = ResolveFromKey(sourceCache);
            cache.Skipped(sourceCache, correlationId);
        }

        /// <inheritdoc/>
        public ResourceType GetResourceTypeByCacheKey(string cacheKey)
        {
            var cache = ResolveFromKey(cacheKey);
            return cache.GetResourceTypeByCacheKey(cacheKey);
        }

        /// <inheritdoc/>
        public void Delete(List<string> cacheKeys)
        {
            var byType = cacheKeys
                .GroupBy(k => _correlationCacheTypes.GetValueOrDefault(ExtractCorrelationId(k), ResourceCacheType.Redis));

            foreach (var group in byType)
            {
                var cache = group.Key == ResourceCacheType.ABS ? _absCache : _redisCache;
                cache.Delete(group.ToList());
            }

            // Clean up recorded decisions for deleted keys
            foreach (var key in cacheKeys)
            {
                _correlationCacheTypes.TryRemove(ExtractCorrelationId(key), out _);
            }
        }

        /// <inheritdoc/>
        public ResourceCacheType GetCacheTypeForCorrelationId(string correlationId)
        {
            return _correlationCacheTypes.GetValueOrDefault(correlationId, ResourceCacheType.Redis);
        }

        /// <inheritdoc/>
        public IResourceCache GetImplementation(ResourceCacheType cacheType)
        {
            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        // -------------------------------------------------------------------------

        private IResourceCache DetermineAndRecordCache(string correlationId)
        {
            var key = ExtractCorrelationId(correlationId);
            var cacheType = _correlationCacheTypes.GetOrAdd(key, _ => SelectCacheType());
            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        private IResourceCache ResolveFromKey(string cacheKey)
        {
            var correlationId = ExtractCorrelationId(cacheKey);
            var cacheType = _correlationCacheTypes.GetValueOrDefault(correlationId, ResourceCacheType.Redis);
            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        private ResourceCacheType SelectCacheType()
        {
            try
            {
                var server = _multiplexer.GetServers().FirstOrDefault(s => s.IsConnected);

                if (server == null)
                {
                    _logger.LogDebug("No connected Redis server found; falling back to ABS resource cache.");
                    return ResourceCacheType.ABS;
                }

                var memoryInfo = server.Info("memory").FirstOrDefault();

                if (memoryInfo == null)
                {
                    _logger.LogDebug("Redis INFO memory returned no results; falling back to ABS resource cache.");
                    return ResourceCacheType.ABS;
                }

                var infoDict = memoryInfo.ToDictionary(e => e.Key, e => e.Value);

                if (!infoDict.TryGetValue("used_memory", out var usedMemoryStr) ||
                    !long.TryParse(usedMemoryStr, out var usedMemory))
                {
                    _logger.LogDebug("Could not parse Redis used_memory; defaulting to Redis resource cache.");
                    return ResourceCacheType.Redis;
                }

                if (!infoDict.TryGetValue("maxmemory", out var maxMemoryStr) ||
                    !long.TryParse(maxMemoryStr, out var maxMemory) ||
                    maxMemory == 0)
                {
                    // No memory limit configured — Redis is unconstrained, always use it.
                    _logger.LogDebug("Redis maxmemory is not configured or unlimited; defaulting to Redis resource cache.");
                    return ResourceCacheType.Redis;
                }

                double usagePercent = (double)usedMemory / maxMemory * 100.0;

                if (usagePercent >= _settings.Redis.MemoryThresholdPercent)
                {
                    _logger.LogDebug(
                        "Redis memory usage {UsagePercent:F1}% meets or exceeds threshold {Threshold}%; using ABS resource cache.",
                        usagePercent, _settings.Redis.MemoryThresholdPercent);
                    return ResourceCacheType.ABS;
                }

                return ResourceCacheType.Redis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Redis memory; falling back to ABS resource cache.");
                return ResourceCacheType.ABS;
            }
        }

        private static string ExtractCorrelationId(string cacheKey)
        {
            var idx = cacheKey.IndexOf(':');
            return idx > 0 ? cacheKey[..idx] : cacheKey;
        }
    }
}
