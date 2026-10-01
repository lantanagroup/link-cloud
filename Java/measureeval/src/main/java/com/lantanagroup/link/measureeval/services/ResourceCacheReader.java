package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.exceptions.ResourceCacheUnavailableException;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.List;

/**
 * Redis-first read over the resource cache with ABS as the durable source — the MeasureEval half
 * (LEGLINK-1279) of the LEGLINK-1118 redesign. Redis is a cache in front of ABS, never the
 * authority, so a Redis miss, eviction or outage is a slower read, never a failure:
 * <ul>
 *   <li>Redis returns resources and the entry is whole — serve them (the common, fast case).</li>
 *   <li>Redis returns resources but fewer than the durable count recorded on the entry — fall back
 *       to ABS. A cache write is a merge that recreates an evicted key, so an entry holding only the
 *       most recent batch is non-empty and otherwise looks whole; the count the durable writer
 *       records once a blob write has landed is what tells them apart (LEGLINK-1276). An entry with
 *       no recorded count is trusted, and so is one holding more than the count, which is the cache
 *       running ahead of a durable write still in flight.</li>
 *   <li>Redis key absent or empty — fall back to ABS. An evicted key is indistinguishable from one
 *       never written, and ABS holds everything by construction.</li>
 *   <li>Redis unreachable — fall back to ABS, not retry. Retrying would burn the retry ladder on a
 *       store that is not the authority.</li>
 * </ul>
 * ABS errors other than blob-not-found still propagate: an unreachable durable store means the
 * resources are unknown, not absent, and the record must retry rather than evaluate a partial or
 * empty bundle (see {@link AbsResourceService#readResources}).
 */
public class ResourceCacheReader {
    private static final Logger logger = LoggerFactory.getLogger(ResourceCacheReader.class);

    private final RedisResourceService redisResourceService;
    private final AbsResourceService absResourceService;

    public ResourceCacheReader(RedisResourceService redisResourceService, AbsResourceService absResourceService) {
        this.redisResourceService = redisResourceService;
        this.absResourceService = absResourceService;
    }

    public List<Resource> readResources(String facilityId, String correlationId, String patientId, String cacheKey) {
        try {
            List<Resource> resources = redisResourceService.readResources(facilityId, cacheKey, patientId);
            if (resources.isEmpty()) {
                logger.debug("Redis miss for cacheKey='{}' (absent, empty or evicted); reading from ABS",
                        LogUtils.sanitize(cacheKey));
            } else if (isWholeEntry(cacheKey, resources.size())) {
                logger.debug("Resource cache hit in Redis for cacheKey='{}' ({} resources)",
                        LogUtils.sanitize(cacheKey), resources.size());
                return resources;
            }
        } catch (ResourceCacheUnavailableException e) {
            logger.warn("Redis unavailable reading cacheKey='{}'; falling back to ABS: {}",
                    LogUtils.sanitize(cacheKey), LogUtils.sanitize(e.getMessage()));
        }
        return absResourceService.readResources(facilityId, correlationId, patientId, cacheKey);
    }

    /**
     * Whether a non-empty cache entry can be trusted as the whole record. Mirrors the .NET
     * {@code HybridResourceCache.IsCacheEntryWholeAsync} rule, including its leniencies: no recorded
     * count, a count the entry meets or exceeds, or a failure to read the count all trust the entry.
     * Only an entry holding fewer resources than the durable store is rejected.
     * <p>
     * {@code cachedCount} is the number of resources the read parsed, so a field skipped as an
     * unknown resource type also lowers it. That can only make the check stricter — a needless ABS
     * read — never let a partial entry through.
     */
    private boolean isWholeEntry(String cacheKey, int cachedCount) {
        Integer durableCount;
        try {
            durableCount = redisResourceService.readDurableResourceCount(cacheKey);
        } catch (RuntimeException e) {
            logger.warn("Could not read the durable resource count for cacheKey='{}'; treating the cache entry as whole: {}",
                    LogUtils.sanitize(cacheKey), LogUtils.sanitize(e.getMessage()));
            return true;
        }

        if (durableCount == null || cachedCount >= durableCount) {
            return true;
        }

        logger.warn("Cache entry for cacheKey='{}' holds {} of {} resources, so it was recreated after an eviction "
                        + "and is not the whole record. Reading ABS instead.",
                LogUtils.sanitize(cacheKey), cachedCount, durableCount);
        return false;
    }
}
