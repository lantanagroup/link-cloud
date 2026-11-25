package com.lantanagroup.link.shared.audit;

import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;

class AuditMetricsTest {
    private final RecordedCounter counter = new RecordedCounter();

    @Test
    void publishFailed_addsOneWithResourceAttribute() {
        new AuditMetrics(counter.openTelemetry(), "Validation").publishFailed("Category");

        assertEquals("audit_publish_failed", counter.counterName());
        assertEquals("Link.Validation", counter.meterName());
        assertEquals(1L, counter.total());
        assertEquals(List.of("Category"), counter.resources());
    }

    @Test
    void publishFailed_nullResource_isUnknown() {
        new AuditMetrics(counter.openTelemetry(), "Validation").publishFailed(null);

        assertEquals(List.of("unknown"), counter.resources());
    }

    @Test
    void blankServiceName_usesAuditMeter() {
        new AuditMetrics(counter.openTelemetry(), " ").publishFailed("Category");

        assertEquals("Link.Audit", counter.meterName());
    }

    @Test
    void nullOpenTelemetry_doesNotThrow() {
        AuditMetrics metrics = assertDoesNotThrow(() -> new AuditMetrics(null, "Validation"));

        assertDoesNotThrow(() -> metrics.publishFailed("Category"));
    }
}
