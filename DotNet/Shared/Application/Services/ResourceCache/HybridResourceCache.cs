using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Collections.Concurrent;
using Task = System.Threading.Tasks.Task;

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
        private readonly IRedisDatabase _redisDatabase;
        private readonly ResourceCacheSettings _settings;
        private readonly ILogger<HybridResourceCache> _logger;

        private readonly ConcurrentDictionary<string, ResourceCacheType> _correlationCacheTypes = new();

        public HybridResourceCache(
            [FromKeyedServices(ResourceCacheType.Redis)] IResourceCache redisCache,
            [FromKeyedServices(ResourceCacheType.ABS)] IResourceCache absCache,
            IRedisDatabase redisDatabase,
            IOptions<ResourceCacheSettings> settings,
            ILogger<HybridResourceCache> logger)
        {
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
            _absCache = absCache ?? throw new ArgumentNullException(nameof(absCache));
            _redisDatabase = redisDatabase ?? throw new ArgumentNullException(nameof(redisDatabase));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task UpdateCorrelationCacheAsync(string correlationId, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default)
        {
            var cache = await DetermineAndRecordCacheAsync(correlationId, cancellationToken);
            await cache.UpdateCorrelationCacheAsync(correlationId, resources, resourceType, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var cache = ResolveFromKey(cacheKey);
            return await cache.GetAsync(cacheKey, cancellationToken);
        }

        /// <inheritdoc/>
        public ResourceType GetResourceTypeByCacheKey(string cacheKey)
        {
            var cache = ResolveFromKey(cacheKey);
            return cache.GetResourceTypeByCacheKey(cacheKey);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            var byType = cacheKeys
                .GroupBy(k => _correlationCacheTypes.GetValueOrDefault(ExtractCorrelationId(k), ResourceCacheType.Redis));

            foreach (var group in byType)
            {
                var cache = group.Key == ResourceCacheType.ABS ? _absCache : _redisCache;
                await cache.DeleteAsync(group.ToList(), cancellationToken);
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

        private async Task<IResourceCache> DetermineAndRecordCacheAsync(string correlationId, CancellationToken cancellationToken)
        {
            var key = ExtractCorrelationId(correlationId);
            if (!_correlationCacheTypes.TryGetValue(key, out var cacheType))
            {
                cacheType = await SelectCacheTypeAsync(cancellationToken);
                cacheType = _correlationCacheTypes.GetOrAdd(key, cacheType);
            }

            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        private IResourceCache ResolveFromKey(string cacheKey)
        {
            var correlationId = ExtractCorrelationId(cacheKey);
            var cacheType = _correlationCacheTypes.GetValueOrDefault(correlationId, ResourceCacheType.Redis);
            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        private async Task<ResourceCacheType> SelectCacheTypeAsync(CancellationToken cancellationToken)
        {
            try
            {
                var server = _redisDatabase.Database.Multiplexer.GetServers().FirstOrDefault(s => s.IsConnected);

                if (server == null)
                {
                    _logger.LogDebug("No connected Redis server found; falling back to ABS resource cache.");
                    return ResourceCacheType.ABS;
                }

                var memoryInfo = (await server.InfoAsync("memory").WaitAsync(cancellationToken)).FirstOrDefault();

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
