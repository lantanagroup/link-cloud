package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.CacheType;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;

class ResourceCacheCleanupTest {

    @Test
    void cleanup_absCacheTypeWithoutConfiguredService_doesNothing() {
        // The ABS branch's null-guard: cache-blob-storage may be unconfigured while a record still
        // arrives claiming CacheType.ABS. Cleanup must no-op (not NPE), and must not fall through
        // to Redis - the entry is left to the cache expiration policy.
        RedisResourceService redis = mock(RedisResourceService.class);
        ResourceCacheCleanup cleanup = new ResourceCacheCleanup(redis, null);

        assertDoesNotThrow(() -> cleanup.cleanup("corr-1", CacheType.ABS));

        verifyNoInteractions(redis);
    }

    @Test
    void cleanup_absCacheTypeWithConfiguredService_cleansAbsOnly() {
        RedisResourceService redis = mock(RedisResourceService.class);
        AbsResourceService abs = mock(AbsResourceService.class);
        ResourceCacheCleanup cleanup = new ResourceCacheCleanup(redis, abs);

        cleanup.cleanup("corr-1", CacheType.ABS);

        verify(abs).cleanup("corr-1");
        verifyNoInteractions(redis);
    }
}
