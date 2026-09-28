using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    /// <summary>
    /// Writes every correlation to blob storage as the durable source and keeps Redis in front of it
    /// as a cache, so a Redis miss, eviction or outage is a slower read rather than a failure.
    /// </summary>
    /// <remarks>
    /// The Redis write is inline; the blob write is queued and completes in the background, which is
    /// why callers must await <see cref="WaitForDurableAsync"/> before advertising a correlation's
    /// cache keys. See docs-dev/resource-cache.md.
    /// </remarks>
    public class HybridResourceCache : IResourceCache
    {
        private readonly IResourceCache _redisCache;
        private readonly IResourceCache _absCache;
        private readonly IBackgroundAbsCacheWriter _absWriter;
        private readonly ILogger<HybridResourceCache> _logger;

        public HybridResourceCache(
            [FromKeyedServices(ResourceCacheType.Redis)] IResourceCache redisCache,
            [FromKeyedServices(ResourceCacheType.ABS)] IResourceCache absCache,
            IBackgroundAbsCacheWriter absWriter,
            ILogger<HybridResourceCache> logger)
        {
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
            _absCache = absCache ?? throw new ArgumentNullException(nameof(absCache));
            _absWriter = absWriter ?? throw new ArgumentNullException(nameof(absWriter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task UpdateCorrelationCacheAsync(string correlationId, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default)
        {
            if (resources == null || resources.Count == 0)
            {
                return;
            }

            try
            {
                await _redisCache.UpdateCorrelationCacheAsync(correlationId, resources, resourceType, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A half-written key would silently win over the complete durable copy, because reads
                // prefer the cache. Drop it instead, so a present entry always holds the whole set.
                _logger.LogWarning(
                    ex,
                    "Could not cache resources for {CacheKey}; dropping the cache entry so it cannot serve a partial read. The durable write is unaffected.",
                    correlationId.SanitizeForLog());

                await TryDropCacheEntryAsync(correlationId, cancellationToken);
            }

            await _absWriter.EnqueueAsync(correlationId, resources, resourceType, cancellationToken);
        }

        /// <inheritdoc/>
        public Task WaitForDurableAsync(string correlationId, CancellationToken cancellationToken = default)
        {
            return _absWriter.WaitForCorrelationAsync(correlationId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var cached = await TryReadCacheAsync(cacheKey, cancellationToken);
            if (cached.Count > 0)
            {
                return cached;
            }

            var durable = await _absCache.GetAsync(cacheKey, cancellationToken) ?? [];
            if (durable.Count == 0)
            {
                return durable;
            }

            await TryRepopulateCacheAsync(cacheKey, durable, cancellationToken);
            return durable;
        }

        /// <inheritdoc/>
        public async Task<bool> HasResourcesAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _redisCache.HasResourcesAsync(cacheKey, cancellationToken))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Cache unavailable while testing {CacheKey}; falling back to durable storage.", cacheKey.SanitizeForLog());
            }

            return await _absCache.HasResourcesAsync(cacheKey, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            if (cacheKeys == null || cacheKeys.Count == 0)
            {
                return;
            }

            // Ahead of both deletes: a write still on the queue would otherwise recreate what is
            // being removed.
            _absWriter.Cancel(cacheKeys);

            try
            {
                await _redisCache.DeleteAsync(cacheKeys, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The cache entry expires on its own, and the durable delete below is what matters.
                _logger.LogWarning(ex, "Could not clear cache entries for [{CacheKeys}]; they will expire.", string.Join(", ", cacheKeys).SanitizeForLog());
            }

            await _absCache.DeleteAsync(cacheKeys, cancellationToken);
        }

        /// <inheritdoc/>
        public ResourceType GetResourceTypeByCacheKey(string cacheKey)
        {
            return _absCache.GetResourceTypeByCacheKey(cacheKey);
        }

        // -------------------------------------------------------------------------

        private async Task<List<DomainResource>> TryReadCacheAsync(string cacheKey, CancellationToken cancellationToken)
        {
            try
            {
                return await _redisCache.GetAsync(cacheKey, cancellationToken) ?? [];
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Cache unavailable reading {CacheKey}; falling back to durable storage.", cacheKey.SanitizeForLog());
                return [];
            }
        }

        private async Task TryRepopulateCacheAsync(string cacheKey, List<DomainResource> resources, CancellationToken cancellationToken)
        {
            try
            {
                var resourceType = _absCache.GetResourceTypeByCacheKey(cacheKey);
                await _redisCache.UpdateCorrelationCacheAsync(cacheKey, resources, resourceType, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best effort. The caller already has its resources; the next reader just pays for
                // another durable read.
                _logger.LogDebug(ex, "Could not repopulate the cache for {CacheKey} after a durable read.", cacheKey.SanitizeForLog());
            }
        }

        private async Task TryDropCacheEntryAsync(string cacheKey, CancellationToken cancellationToken)
        {
            try
            {
                await _redisCache.DeleteAsync([cacheKey], cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not drop the partial cache entry for {CacheKey}; it will expire.", cacheKey.SanitizeForLog());
            }
        }
    }
}
