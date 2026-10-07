package com.lantanagroup.link.shared.config;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("telemetry")
@Getter @Setter
public class TelemetryConfig {
    private String exporterEndpoint;

    /**
     * The environment this process runs in, sent as the {@code deployment.environment.name} resource
     * attribute. Empty leaves the attribute off.
     */
    private String deploymentEnvironment;
}
