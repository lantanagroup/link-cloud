package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.shared.utils.DiagnosticNames;
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
import static org.junit.jupiter.api.Assertions.assertFalse;
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

    // ----- the read instrument is shared with the .NET services -----
    //
    // Both runtimes have to omit an absent tag rather than record it empty: an empty label exports as
    // a different series from an absent one, so a query written against one runtime would silently
    // miss the other. ResourceCacheReaderTest mocks this class, so nothing there proves it.

    @Test
    void recordResourceCacheRead_hit_omitsTheFallbackReasonRatherThanRecordingItEmpty() {
        metrics.recordResourceCacheRead("hit", null, "Initial", 12.5);

        var attributes = singleReadAttributes();
        assertEquals("hit", attributes.get(stringKey(MeasureEvalMetrics.CACHE_OUTCOME)));
        assertFalse(attributes.asMap().containsKey(stringKey(MeasureEvalMetrics.CACHE_FALLBACK_REASON)),
                "a hit must carry no cache.fallback.reason key at all");
    }

    @Test
    void recordResourceCacheRead_fallback_carriesTheReason() {
        metrics.recordResourceCacheRead("fallback", "partial", "Supplemental", 12.5);

        var attributes = singleReadAttributes();
        assertEquals("fallback", attributes.get(stringKey(MeasureEvalMetrics.CACHE_OUTCOME)));
        assertEquals("partial", attributes.get(stringKey(MeasureEvalMetrics.CACHE_FALLBACK_REASON)));
    }

    @Test
    void recordResourceCacheRead_withoutAPhase_omitsThePhaseTag() {
        metrics.recordResourceCacheRead("fallback", "miss", null, 12.5);

        // The .NET readers have no phase concept, so every cross-runtime query aggregates over it.
        // A phase recorded empty here would make that aggregation disagree with this series.
        assertFalse(singleReadAttributes().asMap().containsKey(stringKey(DiagnosticNames.PHASE)),
                "an unknown phase must carry no phase key at all");
    }

    @Test
    void incrementDurableCountReadFailure_withoutAPhase_omitsThePhaseTag() {
        metrics.incrementDurableCountReadFailure(null);

        MetricData metric = exported(MeasureEvalMetrics.DURABLE_COUNT_READ_FAILURE_COUNT);
        LongPointData point = metric.getLongSumData().getPoints().iterator().next();

        assertEquals(1L, point.getValue());
        assertTrue(point.getAttributes().isEmpty(), "an unphased failure carries no tags");
    }

    private Attributes singleReadAttributes() {
        MetricData metric = exported(MeasureEvalMetrics.RESOURCE_CACHE_READ_DURATION);
        var points = metric.getHistogramData().getPoints();

        assertEquals(1, points.size(), "one read, one series");
        return points.iterator().next().getAttributes();
    }

    private MetricData exported(String name) {
        return reader.collectAllMetrics().stream()
                .filter(data -> data.getName().equals(name))
                .findFirst()
                .orElseThrow(() -> new AssertionError(name + " was not exported"));
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
