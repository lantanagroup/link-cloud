package com.lantanagroup.link.validation.config;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "artifact")
@Getter
@Setter
public class ArtifactConfig {
    private boolean init = true;
}
