package com.lantanagroup.link.validation.configs;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.cache.CacheManager;
import org.springframework.cache.annotation.EnableCaching;
import org.springframework.cache.support.NoOpCacheManager;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
@EnableCaching
public class NoCacheConfig {
    private static final Logger logger = LoggerFactory.getLogger(NoCacheConfig.class);

    @Bean
    @ConditionalOnProperty(name = "cache.type", havingValue = "none")
    public CacheManager noOpCacheManager() {
        logger.info("Cache type set to 'none'");
        return new NoOpCacheManager();
    }
}
