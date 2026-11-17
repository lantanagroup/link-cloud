package com.lantanagroup.link.validation.configs;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConfigurationProperties(prefix = "cache")
public class CacheConfig {
    @Getter @Setter
    private ValidationCacheTypes type = ValidationCacheTypes.NONE;

    @Getter @Setter
    private ValidateCodeConfig validateCode = new ValidateCodeConfig();

    @Getter @Setter
    private RubricConfig rubric = new RubricConfig();

    @Getter
    @Setter
    public static class ValidateCodeConfig {
        private long ttl = 3600;
    }

    @Getter
    @Setter
    public static class RubricConfig {
        private long ttl = 3600;
    }
}
