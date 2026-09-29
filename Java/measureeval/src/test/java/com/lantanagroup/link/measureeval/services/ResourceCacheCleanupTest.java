package com.lantanagroup.link.measureeval.services;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;

/**
 * Since LEGLINK-1279 cleanup covers BOTH stores unconditionally: the data lives in ABS (durable)
 * and usually also in Redis (cache), and no message field says which — so both are cleared, and a
 * failure in one store must never skip the other.
 */
class ResourceCacheCleanupTest {

    @Test
    void cleanup_deletesFromBothStores() {
        RedisResourceService redis = mock(RedisResourceService.class);
        AbsResourceService abs = mock(AbsResourceService.class);
        ResourceCacheCleanup cleanup = new ResourceCacheCleanup(redis, abs);

        cleanup.cleanup("corr-1");

        verify(redis).cleanup("corr-1");
        verify(abs).cleanup("corr-1");
    }

    @Test
    void cleanup_redisFailure_stillCleansAbs() {
        // Separate guards per store: a Redis outage during cleanup must not strand the ABS blobs
        // (they would otherwise wait for the storage lifecycle rule), and cleanup never throws.
        RedisResourceService redis = mock(RedisResourceService.class);
        AbsResourceService abs = mock(AbsResourceService.class);
        doThrow(new RuntimeException("redis down")).when(redis).cleanup("corr-1");
        ResourceCacheCleanup cleanup = new ResourceCacheCleanup(redis, abs);

        assertDoesNotThrow(() -> cleanup.cleanup("corr-1"));

        verify(abs).cleanup("corr-1");
    }

    @Test
    void cleanup_absFailure_neverThrows() {
        RedisResourceService redis = mock(RedisResourceService.class);
        AbsResourceService abs = mock(AbsResourceService.class);
        doThrow(new RuntimeException("abs down")).when(abs).cleanup("corr-1");
        ResourceCacheCleanup cleanup = new ResourceCacheCleanup(redis, abs);

        assertDoesNotThrow(() -> cleanup.cleanup("corr-1"));

        verify(redis).cleanup("corr-1");
    }

    @Test
    void cleanup_nullAbsService_cleansRedisOnly() {
        // Defensive: the ABS bean is required at boot since LEGLINK-1279, but cleanup keeps the
        // null-guard so a partially wired context can never NPE its way out of a Redis delete.
        RedisResourceService redis = mock(RedisResourceService.class);
        ResourceCacheCleanup cleanup = new ResourceCacheCleanup(redis, null);

        assertDoesNotThrow(() -> cleanup.cleanup("corr-1"));

        verify(redis).cleanup("corr-1");
    }

    @Test
    void cleanup_nullOrEmptyCorrelationId_doesNothing() {
        RedisResourceService redis = mock(RedisResourceService.class);
        AbsResourceService abs = mock(AbsResourceService.class);
        ResourceCacheCleanup cleanup = new ResourceCacheCleanup(redis, abs);

        cleanup.cleanup(null);
        cleanup.cleanup("");

        verifyNoInteractions(redis);
        verifyNoInteractions(abs);
    }
}
