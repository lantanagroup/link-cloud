package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Releases the resource cache entries for a correlation id from BOTH stores. Since LEGLINK-1279
 * the data lives in ABS (durable) and usually also in Redis (cache), so cleanup no longer picks a
 * store by {@code CacheType} — it deletes from both, each guarded separately so a failure in one
 * store can never skip the other. Deleting a key that is not there is free.
 *
 * <p>Shared by the two places allowed to delete: the consumer's success path (the record was fully
 * evaluated) and the recoverer's terminal-failure hook (the record was durably dead-lettered and
 * will never be redelivered).</p>
 *
 * <p>Never call this on a failure that may still be retried — the redelivered record needs its
 * cached resources. Best-effort: failures are logged, never thrown, so cleanup can never turn a
 * handled record back into a failure; the Redis TTL and the ABS lifecycle policy are the backstop.</p>
 */
public class ResourceCacheCleanup {

    private static final Logger logger = LoggerFactory.getLogger(ResourceCacheCleanup.class);

    private final RedisResourceService redisResourceService;
    private final AbsResourceService absResourceService;

    public ResourceCacheCleanup(RedisResourceService redisResourceService, AbsResourceService absResourceService) {
        this.redisResourceService = redisResourceService;
        this.absResourceService = absResourceService;
    }

    public void cleanup(String correlationId) {
        if (correlationId == null || correlationId.isEmpty()) {
            return;
        }
        try {
            redisResourceService.cleanup(correlationId);
        } catch (Exception e) {
            logger.error("Redis cache cleanup failed for correlationId={}", LogUtils.sanitize(correlationId), e);
        }
        try {
            if (absResourceService != null) {
                absResourceService.cleanup(correlationId);
            }
        } catch (Exception e) {
            logger.error("ABS cache cleanup failed for correlationId={}", LogUtils.sanitize(correlationId), e);
        }
        logger.debug("Cache cleanup complete for correlationId={}", LogUtils.sanitize(correlationId));
    }
}
