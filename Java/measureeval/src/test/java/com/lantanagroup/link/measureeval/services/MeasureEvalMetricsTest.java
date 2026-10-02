package com.lantanagroup.link.measureeval.services;

import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.sdk.OpenTelemetrySdk;
import io.opentelemetry.sdk.metrics.SdkMeterProvider;
import io.opentelemetry.sdk.metrics.data.LongPointData;
import io.opentelemetry.sdk.metrics.data.MetricData;
import io.opentelemetry.sdk.testing.exporter.InMemoryMetricReader;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.Map;
import java.util.stream.Collectors;

import static io.opentelemetry.api.common.AttributeKey.stringKey;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Run metrics count a stage's errors by filtering its counter on {@code outcome="failure"}. The
 * evaluation counter carried no outcome and was only ever incremented on success, so MeasureEval's
 * error count could never be anything but zero.
 */
class MeasureEvalMetricsTest {

    private static final String EVAL_COUNT = "link_measureeval_eval_count";

    private InMemoryMetricReader reader;
    private MeasureEvalMetrics metrics;

    @BeforeEach
    void setup() {
        reader = InMemoryMetricReader.create();
        var meterProvider = SdkMeterProvider.builder().registerMetricReader(reader).build();
        metrics = new MeasureEvalMetrics(OpenTelemetrySdk.builder().setMeterProvider(meterProvider).build());
    }

    @Test
    void measureEvalDuration_successfulEvaluation_countsWithSuccessOutcome() {
        metrics.MeasureEvalDuration(25, Attributes.of(stringKey("facility.id"), "facility-1"));

        assertEquals(Map.of("success", 1L), countsByOutcome());
    }

    @Test
    void recordEvaluationFailure_failedEvaluation_countsWithFailureOutcome() {
        var attributes = Attributes.of(stringKey("facility.id"), "facility-1");

        metrics.MeasureEvalDuration(25, attributes);
        metrics.recordEvaluationFailure(attributes);
        metrics.recordEvaluationFailure(attributes);

        assertEquals(Map.of("success", 1L, "failure", 2L), countsByOutcome());
    }

    @Test
    void recordEvaluationFailure_failedEvaluation_keepsTheCallersAttributes() {
        metrics.recordEvaluationFailure(Attributes.of(stringKey("facility.id"), "facility-1"));

        var point = evalCountPoints().iterator().next();
        assertEquals("facility-1", point.getAttributes().get(stringKey("facility.id")));
    }

    private Map<String, Long> countsByOutcome() {
        return evalCountPoints().stream()
                .collect(Collectors.toMap(
                        point -> point.getAttributes().get(stringKey(MeasureEvalMetrics.OUTCOME)),
                        LongPointData::getValue));
    }

    private java.util.Collection<LongPointData> evalCountPoints() {
        MetricData metric = reader.collectAllMetrics().stream()
                .filter(data -> data.getName().equals(EVAL_COUNT))
                .findFirst()
                .orElseThrow(() -> new AssertionError(EVAL_COUNT + " was not exported"));

        var points = metric.getLongSumData().getPoints();
        assertTrue(points.stream().allMatch(point -> point.getAttributes().get(stringKey(MeasureEvalMetrics.OUTCOME)) != null),
                "every evaluation is tagged with its outcome");
        return points;
    }
}
