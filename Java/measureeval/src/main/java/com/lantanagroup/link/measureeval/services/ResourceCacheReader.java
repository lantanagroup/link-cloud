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
 *   <li>Redis returns resources — serve them (the common, fast case).</li>
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

    /**
     * Reads straight from ABS, bypassing Redis. Used for the terminal (SUPPLEMENTAL) evaluation:
     * that read produces the submitted report, and its correlation hash has idled through the
     * whole initial→supplemental gap — the prime eviction window. A key evicted there and then
     * recreated by the supplemental append reads as present-but-partial, which Redis-first would
     * trust; ABS is complete by construction. Once Normalization deletes the Redis key on the
     * supplemental pass (proposed on LEGLINK-1276), the Redis read would always miss here anyway,
     * so this also saves a guaranteed-miss round trip.
     */
    public List<Resource> readResourcesDurable(String facilityId, String correlationId, String patientId, String cacheKey) {
        return absResourceService.readResources(facilityId, correlationId, patientId, cacheKey);
    }

    public List<Resource> readResources(String facilityId, String correlationId, String patientId, String cacheKey) {
        try {
            List<Resource> resources = redisResourceService.readResources(facilityId, cacheKey, patientId);
            if (!resources.isEmpty()) {
                logger.debug("Resource cache hit in Redis for cacheKey='{}' ({} resources)",
                        LogUtils.sanitize(cacheKey), resources.size());
                return resources;
            }
            logger.debug("Redis miss for cacheKey='{}' (absent, empty or evicted); reading from ABS",
                    LogUtils.sanitize(cacheKey));
        } catch (ResourceCacheUnavailableException e) {
            logger.warn("Redis unavailable reading cacheKey='{}'; falling back to ABS: {}",
                    LogUtils.sanitize(cacheKey), LogUtils.sanitize(e.getMessage()));
        }
        return absResourceService.readResources(facilityId, correlationId, patientId, cacheKey);
    }
}
