using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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
        private readonly IResourceCacheMetrics _metrics;
        private readonly ILogger<HybridResourceCache> _logger;

        public HybridResourceCache(
            [FromKeyedServices(ResourceCacheType.Redis)] IResourceCache redisCache,
            [FromKeyedServices(ResourceCacheType.ABS)] IResourceCache absCache,
            IBackgroundAbsCacheWriter absWriter,
            IResourceCacheMetrics metrics,
            ILogger<HybridResourceCache> logger)
        {
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
            _absCache = absCache ?? throw new ArgumentNullException(nameof(absCache));
            _absWriter = absWriter ?? throw new ArgumentNullException(nameof(absWriter));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task AppendResourcesAsync(string cacheKey, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default)
        {
            if (resources == null || resources.Count == 0)
            {
                return;
            }

            var writeStart = Stopwatch.GetTimestamp();
            try
            {
                await _redisCache.AppendResourcesAsync(cacheKey, resources, resourceType, cancellationToken);
                _metrics.RecordWrite(ResourceCacheStores.Redis, ResourceCacheOutcomes.Ok, Elapsed(writeStart));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _metrics.RecordWrite(ResourceCacheStores.Redis, ResourceCacheOutcomes.Failed, Elapsed(writeStart));

                // A half-written key would silently win over the complete durable copy, because reads
                // prefer the cache. Drop it instead, so a present entry always holds the whole set.
                _logger.LogWarning(
                    ex,
                    "Could not cache resources for {CacheKey}; dropping the cache entry so it cannot serve a partial read. The durable write is unaffected.",
                    cacheKey.SanitizeForLog());

                await TryDropCacheEntryAsync(cacheKey, cancellationToken);
            }

            await _absWriter.EnqueueAsync(cacheKey, resources, resourceType, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task WaitForDurableAsync(string correlationId, CancellationToken cancellationToken = default)
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                await _absWriter.WaitForCorrelationAsync(correlationId, cancellationToken);
            }
            finally
            {
                // Recorded even when the barrier throws: a caller that waited and then failed still
                // paid the time, and hiding it would flatter the measurement.
                _metrics.RecordDrainWait(Elapsed(start));
            }
        }

        /// <inheritdoc/>
        public async Task WaitForDurableAsync(IEnumerable<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                await _absWriter.WaitForDurableAsync(cacheKeys, cancellationToken);
            }
            finally
            {
                _metrics.RecordDrainWait(Elapsed(start));
            }
        }

        /// <inheritdoc/>
        public async Task WaitForDurableAsync(IEnumerable<string> cacheKeys,
                                              IReadOnlyCollection<string> references,
                                              CancellationToken cancellationToken = default)
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                await _absWriter.WaitForDurableAsync(cacheKeys, references, cancellationToken);
            }
            finally
            {
                _metrics.RecordDrainWait(Elapsed(start));
            }
        }

        /// <inheritdoc/>
        public async Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var readStart = Stopwatch.GetTimestamp();

            var cached = await TryReadCacheAsync(cacheKey, cancellationToken);
            if (cached.Count > 0 && await IsCacheEntryWholeAsync(cacheKey, cached.Count, cancellationToken))
            {
                _metrics.RecordRead(ResourceCacheOutcomes.Hit, Elapsed(readStart));
                return cached;
            }

            var durable = await _absCache.GetAsync(cacheKey, cancellationToken) ?? [];
            if (durable.Count == 0)
            {
                _metrics.RecordRead(ResourceCacheOutcomes.Empty, Elapsed(readStart));
                return durable;
            }

            await TryRepopulateCacheAsync(cacheKey, durable, cancellationToken);
            _metrics.RecordRead(ResourceCacheOutcomes.Fallback, Elapsed(readStart));
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

            // A failed cache delete stops before durable storage is touched. A cache entry that outlives
            // its blob is served whole, so a redelivery could copy one key and find a later evicted one
            // empty, which reads as a producer defect. See docs-dev/resource-cache.md.
            await _redisCache.DeleteAsync(cacheKeys, cancellationToken);

            await _absCache.DeleteAsync(cacheKeys, cancellationToken);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Ordering is the whole design here, so it is spelled out.
        /// <list type="number">
        ///   <item><description>
        ///     Cancel queued writes for the key first, or one of them lands afterwards and merges back
        ///     the content this call exists to remove.
        ///   </description></item>
        ///   <item><description>
        ///     Replace durably and wait for it, rather than queueing it. Queued, a failure between
        ///     clearing and rewriting would leave the durable copy empty with nothing to retry from, and
        ///     the caller is about to cross a barrier for this key anyway.
        ///   </description></item>
        ///   <item><description>
        ///     Replace the cache entry last, atomically. A failure here leaves the cache holding the
        ///     pre-replace content, which is what the retry reads to work out what to do -- the reverse
        ///     order would leave the cache already replaced and the durable copy stale, and the retry
        ///     would see nothing left to remove and stop.
        ///   </description></item>
        ///   <item><description>
        ///     Record the new durable count, so the entry does not read as partial against the count
        ///     from before the replace.
        ///   </description></item>
        /// </list>
        /// Nothing here is tolerated: a replace that cannot clear is not a replace, and the callers that
        /// need one are the ones where leaving the old content in place is the failure.
        /// </remarks>
        public async Task ReplaceResourcesAsync(
            string cacheKey,
            List<DomainResource> resources,
            ResourceType resourceType,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(cacheKey);

            // Drained, not just cancelled. A write already executing has passed every generation
            // check, and on finishing it sees the key was cancelled and deletes it from durable
            // storage -- which would take the replacement with it, since the replacement is written
            // below and lands first. Waiting until nothing is writing this key makes the replace the
            // last word on it.
            await _absWriter.CancelAndDrainAsync([cacheKey], cancellationToken);

            // Both stores are written, so both are counted. Recording one and not the other would
            // break the pairing the write metrics exist to assert.
            var durableStart = Stopwatch.GetTimestamp();
            await _absCache.ReplaceResourcesAsync(cacheKey, resources, resourceType, cancellationToken);
            _metrics.RecordWrite(ResourceCacheStores.Blob, ResourceCacheOutcomes.Ok, Elapsed(durableStart));

            var cacheStart = Stopwatch.GetTimestamp();
            await _redisCache.ReplaceResourcesAsync(cacheKey, resources, resourceType, cancellationToken);
            _metrics.RecordWrite(ResourceCacheStores.Redis, ResourceCacheOutcomes.Ok, Elapsed(cacheStart));

            var replacedCount = resources?.Count ?? 0;
            if (replacedCount > 0)
            {
                await _redisCache.SetDurableResourceCountAsync(cacheKey, replacedCount, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public ResourceType GetResourceTypeByCacheKey(string cacheKey)
        {
            return _absCache.GetResourceTypeByCacheKey(cacheKey);
        }

        // -------------------------------------------------------------------------

        private static double Elapsed(long startTimestamp) =>
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        /// <inheritdoc/>
        public async Task<int> GetResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var cached = await _redisCache.GetResourceCountAsync(cacheKey, cancellationToken);
                if (cached > 0)
                {
                    return cached;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Cache unavailable counting {CacheKey}; falling back to durable storage.", cacheKey.SanitizeForLog());
            }

            return await _absCache.GetResourceCountAsync(cacheKey, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<int?> GetDurableResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            _redisCache.GetDurableResourceCountAsync(cacheKey, cancellationToken);

        /// <inheritdoc/>
        public Task SetDurableResourceCountAsync(string cacheKey, int count, CancellationToken cancellationToken = default) =>
            _redisCache.SetDurableResourceCountAsync(cacheKey, count, cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> IsEntryCompleteAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            int cachedCount;

            try
            {
                cachedCount = await _redisCache.GetResourceCountAsync(cacheKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Cache unavailable counting {CacheKey}; treating the entry as incomplete so the caller reads through.",
                    cacheKey.SanitizeForLog());
                return false;
            }

            // Nothing cached is not the whole record unless durable storage holds nothing either, and
            // that costs the same read the caller is trying to avoid. Reading through settles it.
            return cachedCount > 0 && await IsCacheEntryWholeAsync(cacheKey, cachedCount, cancellationToken);
        }

        /// <summary>
        /// Whether a non-empty cache entry can be trusted as the whole record.
        /// </summary>
        /// <remarks>
        /// A cache write is a merge that recreates an evicted key, so an entry holding only the most
        /// recent batch looks exactly like a whole one. The count recorded once a durable write landed is
        /// what tells them apart. An entry with no recorded count is trusted: nothing has completed a
        /// durable write for that key, so durable storage has no more to offer and falling back would
        /// turn a usable entry into an empty read.
        /// </remarks>
        private async Task<bool> IsCacheEntryWholeAsync(string cacheKey, int cachedCount, CancellationToken cancellationToken)
        {
            int? durableCount;

            try
            {
                durableCount = await _redisCache.GetDurableResourceCountAsync(cacheKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Could not read the durable resource count for {CacheKey}; treating the cache entry as whole.",
                    cacheKey.SanitizeForLog());
                return true;
            }

            if (durableCount is null || cachedCount >= durableCount.Value)
            {
                return true;
            }

            _logger.LogWarning(
                "Cache entry for {CacheKey} holds {CachedCount} of {DurableCount} resources, so it was "
                + "recreated after an eviction and is not the whole record. Reading durable storage instead.",
                cacheKey.SanitizeForLog(),
                cachedCount,
                durableCount.Value);

            return false;
        }

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
                // Only the acquisition keys carry a resource type. A correlation key is a bare id,
                // and GetResourceTypeByCacheKey throws on one -- which the catch below would swallow,
                // leaving the entry unrestored for exactly the key a two-pass evaluation depends on.
                // The cache does not use the value: it names each field after the resource it holds.
                var resourceType = cacheKey.Contains(':')
                    ? _absCache.GetResourceTypeByCacheKey(cacheKey)
                    : ResourceType.Bundle;

                await _redisCache.AppendResourcesAsync(cacheKey, resources, resourceType, cancellationToken);

                // Recorded with the entry it describes. Without this the restored entry has no count,
                // and a later partial recreation of it could not be detected.
                await _redisCache.SetDurableResourceCountAsync(cacheKey, resources.Count, cancellationToken);
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
