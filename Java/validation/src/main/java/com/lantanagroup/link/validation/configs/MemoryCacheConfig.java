package com.lantanagroup.link.validation.configs;

import com.github.benmanes.caffeine.cache.Caffeine;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.cache.CacheManager;
import org.springframework.cache.annotation.EnableCaching;
import org.springframework.cache.caffeine.CaffeineCacheManager;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.time.Duration;

@Configuration
@EnableCaching
public class MemoryCacheConfig {
    private static final Logger logger = LoggerFactory.getLogger(MemoryCacheConfig.class);

    private final CacheConfig cacheConfig;

    public MemoryCacheConfig(CacheConfig cacheConfig) {
        this.cacheConfig = cacheConfig;
    }

    @Bean
    @ConditionalOnProperty(name = "cache.type", havingValue = "memory")
    public CacheManager caffeineCacheManager() {
        logger.info("Cache type set to 'memory'");
        CaffeineCacheManager cacheManager = new CaffeineCacheManager("validateCodeCache");
        cacheManager.setCaffeine(Caffeine.newBuilder()
                .expireAfterWrite(Duration.ofSeconds(this.cacheConfig.getValidateCode().getTtl()))
                .maximumSize(1000));
        return cacheManager;
    }
}
