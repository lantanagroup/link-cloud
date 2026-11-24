package com.lantanagroup.link.shared.audit;

import io.opentelemetry.api.common.AttributeKey;
import io.opentelemetry.sdk.OpenTelemetrySdk;
import io.opentelemetry.sdk.metrics.SdkMeterProvider;
import io.opentelemetry.sdk.metrics.data.LongPointData;
import io.opentelemetry.sdk.metrics.data.MetricData;
import io.opentelemetry.sdk.testing.exporter.InMemoryMetricReader;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;

class AuditMetricsTest {
    private static final AttributeKey<String> RESOURCE = AttributeKey.stringKey("resource");

    private final InMemoryMetricReader reader = InMemoryMetricReader.create();
    private final OpenTelemetrySdk sdk = OpenTelemetrySdk.builder()
            .setMeterProvider(SdkMeterProvider.builder().registerMetricReader(reader).build())
            .build();

    @Test
    void publishFailed_addsOneWithResourceAttribute() {
        new AuditMetrics(sdk, "Validation").publishFailed("Category");

        MetricData metric = single();
        assertEquals("audit_publish_failed", metric.getName());
        assertEquals("Link.Validation", metric.getInstrumentationScopeInfo().getName());
        LongPointData point = point(metric);
        assertEquals(1L, point.getValue());
        assertEquals("Category", point.getAttributes().get(RESOURCE));
    }

    @Test
    void publishFailed_nullResource_isUnknown() {
        new AuditMetrics(sdk, "Validation").publishFailed(null);

        assertEquals("unknown", point(single()).getAttributes().get(RESOURCE));
    }

    @Test
    void blankServiceName_usesAuditMeter() {
        new AuditMetrics(sdk, " ").publishFailed("Category");

        assertEquals("Link.Audit", single().getInstrumentationScopeInfo().getName());
    }

    @Test
    void nullOpenTelemetry_doesNotThrow() {
        AuditMetrics metrics = assertDoesNotThrow(() -> new AuditMetrics(null, "Validation"));

        assertDoesNotThrow(() -> metrics.publishFailed("Category"));
    }

    private MetricData single() {
        List<MetricData> metrics = List.copyOf(reader.collectAllMetrics());
        assertEquals(1, metrics.size());
        return metrics.get(0);
    }

    private static LongPointData point(MetricData metric) {
        List<LongPointData> points = List.copyOf(metric.getLongSumData().getPoints());
        assertEquals(1, points.size());
        return points.get(0);
    }
}
