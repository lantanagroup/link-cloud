package com.lantanagroup.link.shared.audit;

import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.api.common.AttributeKey;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.metrics.LongCounter;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Counts audit events that could not be published, by resource. Never throws.
 */
public final class AuditMetrics {
    public static final String PUBLISH_FAILED = "audit_publish_failed";

    private static final Logger logger = LoggerFactory.getLogger(AuditMetrics.class);
    private static final AttributeKey<String> RESOURCE = AttributeKey.stringKey("resource");
    private static final String UNKNOWN_RESOURCE = "unknown";

    private final LongCounter publishFailed;

    public AuditMetrics(OpenTelemetry openTelemetry, String serviceName) {
        OpenTelemetry telemetry = openTelemetry;
        if (telemetry == null) {
            logger.warn("No OpenTelemetry instance is available; the {} counter will not be exported", PUBLISH_FAILED);
            telemetry = OpenTelemetry.noop();
        }
        String meterName = serviceName == null || serviceName.isBlank() ? "Link.Audit" : "Link." + serviceName;
        this.publishFailed = telemetry.getMeter(meterName).counterBuilder(PUBLISH_FAILED).build();
    }

    public void publishFailed(String resource) {
        try {
            publishFailed.add(1L, Attributes.of(RESOURCE, resource == null ? UNKNOWN_RESOURCE : resource));
        } catch (RuntimeException e) {
            logger.debug("Could not record the {} counter", PUBLISH_FAILED, e);
        }
    }
}
