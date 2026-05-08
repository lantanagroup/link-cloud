package com.lantanagroup.link.validation.configs;

import org.springframework.boot.autoconfigure.data.redis.RedisProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class RedisPropertiesConfig {
    @Bean
    public RedisProperties redisProperties() {
        return new RedisProperties();
    }
}