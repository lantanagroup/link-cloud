package com.lantanagroup.link.shared.audit;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;

import java.time.Duration;

/**
 * Settings of the shared audit mechanism, under {@code link.audit}.
 */
@ConfigurationProperties("link.audit")
@Getter
@Setter
public class AuditProperties {
    /** The name recorded as {@code ServiceName} on every event. */
    private String serviceName;

    private final Publish publish = new Publish();

    @Getter
    @Setter
    public static class Publish {
        /** How long a request waits for the broker to acknowledge an audit event. */
        private Duration responseTimeout = Duration.ofSeconds(3);
    }
}
