package com.lantanagroup.link.validation.configs;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.cache.Cache;
import org.springframework.cache.annotation.CachingConfigurer;
import org.springframework.cache.interceptor.CacheErrorHandler;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

/**
 * Cache errors (e.g. a Redis timeout) are logged and treated as cache misses instead of failing
 * validation. A failed GET falls back to calling the terminology service; a failed PUT skips
 * caching the result. By default Spring rethrows cache errors, which dead-letters an
 * otherwise-valid ReadyForValidation message. With this handler, a Redis outage will be an
 * uncached validation.
 */
@Configuration
public class CacheErrorHandlingConfig implements CachingConfigurer {
    private static final Logger logger = LoggerFactory.getLogger(CacheErrorHandlingConfig.class);

    // Replaces Spring's default SimpleCacheErrorHandler, which rethrows cache errors, with one that
    // logs and swallows them. Exposed as a bean so ValidationCacheService can use the same policy
    // for programmatic cache get/put (those calls do not go through CacheInterceptor).
    @Bean
    @Override
    public CacheErrorHandler errorHandler() {
        return new CacheErrorHandler() {
            @Override
            public void handleCacheGetError(RuntimeException exception, Cache cache, Object key) {
                logger.warn("Cache '{}' GET failed; calling the terminology service directly: {}",
                        cache.getName(), exception.getMessage());
            }

            @Override
            public void handleCachePutError(RuntimeException exception, Cache cache, Object key, Object value) {
                logger.warn("Cache '{}' PUT failed: {}", cache.getName(), exception.getMessage());
            }

            @Override
            public void handleCacheEvictError(RuntimeException exception, Cache cache, Object key) {
                logger.warn("Cache '{}' EVICT failed: {}", cache.getName(), exception.getMessage());
            }

            @Override
            public void handleCacheClearError(RuntimeException exception, Cache cache) {
                logger.warn("Cache '{}' CLEAR failed: {}", cache.getName(), exception.getMessage());
            }
        };
    }
}
