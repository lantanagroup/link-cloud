package com.lantanagroup.link.validation.configs;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.cache.Cache;
import org.springframework.cache.interceptor.CacheErrorHandler;
import org.springframework.dao.QueryTimeoutException;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Verifies that cache-backend failures are swallowed rather than rethrown, so a Redis
 * timeout becomes a cache miss instead of dead-lettering the ReadyForValidation
 * message. See LEGLINK-634.
 */
class CacheErrorHandlingConfigTest {

    // The exact failure observed in the ReadyForValidation-Error dead-letter messages.
    private static final QueryTimeoutException REDIS_TIMEOUT =
            new QueryTimeoutException("Redis command timed out");

    private CacheErrorHandler handler;
    private Cache cache;

    @BeforeEach
    void setUp() {
        handler = new CacheErrorHandlingConfig().errorHandler();
        cache = mock(Cache.class);
        when(cache.getName()).thenReturn("validateCodeCache");
    }

    @Test
    void errorHandler_isProvided() {
        assertNotNull(handler, "config must supply a CacheErrorHandler (replacing SimpleCacheErrorHandler)");
    }

    @Test
    void handleCacheGetError_swallowsBackendFailure() {
        // Swallowing lets the caching interceptor treat the failure as a miss and invoke the
        // underlying method; rethrowing here is what dead-lettered valid messages.
        assertDoesNotThrow(() -> handler.handleCacheGetError(REDIS_TIMEOUT, cache, "some-key"));
    }

    @Test
    void handleCachePutError_swallowsBackendFailure() {
        assertDoesNotThrow(() -> handler.handleCachePutError(REDIS_TIMEOUT, cache, "some-key", "some-value"));
    }

    @Test
    void handleCacheEvictError_swallowsBackendFailure() {
        assertDoesNotThrow(() -> handler.handleCacheEvictError(REDIS_TIMEOUT, cache, "some-key"));
    }

    @Test
    void handleCacheClearError_swallowsBackendFailure() {
        assertDoesNotThrow(() -> handler.handleCacheClearError(REDIS_TIMEOUT, cache));
    }

    @Test
    void handlers_swallowArbitraryRuntimeExceptions() {
        // Not just timeouts: connection failures and any other backend RuntimeException must also
        // be non-fatal (e.g. RedisConnectionFailureException when Redis is down entirely).
        RuntimeException connectionFailure = new RuntimeException("Unable to connect to Redis");
        assertDoesNotThrow(() -> handler.handleCacheGetError(connectionFailure, cache, "k"));
        assertDoesNotThrow(() -> handler.handleCachePutError(connectionFailure, cache, "k", "v"));
    }
}
