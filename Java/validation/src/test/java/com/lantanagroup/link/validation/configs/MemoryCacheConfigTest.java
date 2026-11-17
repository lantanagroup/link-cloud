package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.validation.providers.RubricCacheService;
import org.junit.jupiter.api.Test;
import org.springframework.cache.Cache;
import org.springframework.cache.CacheManager;

import static org.assertj.core.api.Assertions.assertThat;

class MemoryCacheConfigTest {

    @Test
    void caffeineCacheManagerRegistersTheFixedValidationCachesAndTheRubricCaches() {
        CacheConfig cacheConfig = new CacheConfig();
        cacheConfig.getValidateCode().setTtl(60);
        cacheConfig.getRubric().setTtl(120);

        CacheManager cacheManager = new MemoryCacheConfig(cacheConfig).caffeineCacheManager();

        assertThat(cacheManager.getCacheNames()).containsExactlyInAnyOrder(
                "validateCodeCache", "lookupCodeCache", "isCodeSystemSupportedCache", "isValueSetSupportedCache",
                RubricCacheService.VERSION_CACHE, RubricCacheService.LATEST_SEMVER_CACHE);
    }

    @Test
    void everyRegisteredCacheIsUsable() {
        CacheManager cacheManager = new MemoryCacheConfig(new CacheConfig()).caffeineCacheManager();

        Cache versionCache = cacheManager.getCache(RubricCacheService.VERSION_CACHE);
        assertThat(versionCache).isNotNull();
        versionCache.put("k", "v");
        assertThat(versionCache.get("k").get()).isEqualTo("v");
    }

    @Test
    void aCacheNameOutsideTheRegisteredListDoesNotExist() {
        // Caffeine runs in static mode here: cache names not fixed at build time simply don't exist.
        CacheManager cacheManager = new MemoryCacheConfig(new CacheConfig()).caffeineCacheManager();

        assertThat(cacheManager.getCache("someRandomUnregisteredCache")).isNull();
    }
}
