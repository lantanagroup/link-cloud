package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.CacheType;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Releases the resource cache entry for a correlation id. Shared by the two places allowed to
 * delete it: the consumer's success path (the record was fully evaluated) and the recoverer's
 * terminal-failure hook (the record was durably dead-lettered and will never be redelivered).
 *
 * <p>Never call this on a failure that may still be retried — the redelivered record needs its
 * cached resources. Best-effort: failures are logged, never thrown, so cleanup can never turn a
 * handled record back into a failure; the cache expiration policy is the backstop.</p>
 */
public class ResourceCacheCleanup {

    private static final Logger logger = LoggerFactory.getLogger(ResourceCacheCleanup.class);

    private final RedisResourceService redisResourceService;
    private final AbsResourceService absResourceService;

    public ResourceCacheCleanup(RedisResourceService redisResourceService, AbsResourceService absResourceService) {
        this.redisResourceService = redisResourceService;
        this.absResourceService = absResourceService;
    }

    public void cleanup(String correlationId, CacheType cacheType) {
        if (correlationId == null || correlationId.isEmpty() || cacheType == null) {
            return;
        }
        try {
            switch (cacheType) {
                case REDIS -> redisResourceService.cleanup(correlationId);
                case ABS -> {
                    if (absResourceService != null) {
                        absResourceService.cleanup(correlationId);
                    }
                }
            }
            logger.debug("Cache cleanup complete for correlationId={}, cacheType={}",
                    LogUtils.sanitize(correlationId), LogUtils.sanitize(cacheType));
        } catch (Exception e) {
            logger.error("Cache cleanup failed for correlationId={}, cacheType={}",
                    LogUtils.sanitize(correlationId), LogUtils.sanitize(cacheType), e);
        }
    }
}
