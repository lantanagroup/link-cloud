package com.lantanagroup.link.shared.config;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.validation.annotation.Validated;

@Getter @Setter
@ConfigurationProperties(prefix = "loki")
public class LokiConfig {
    private boolean enabled = false;
    private String url;
}
