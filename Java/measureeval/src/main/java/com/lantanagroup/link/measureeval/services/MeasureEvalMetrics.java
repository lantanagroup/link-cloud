package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.shared.utils.DiagnosticNames;
import com.lantanagroup.link.shared.utils.HistogramBuckets;
import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.common.AttributesBuilder;
import io.opentelemetry.api.metrics.DoubleHistogram;
import io.opentelemetry.api.metrics.LongCounter;
import io.opentelemetry.api.metrics.LongHistogram;
import io.opentelemetry.api.metrics.Meter;
import org.springframework.stereotype.Service;

import static com.lantanagroup.link.shared.utils.StringUtils.safe;
import static io.opentelemetry.api.common.AttributeKey.stringKey;


@Service
public class MeasureEvalMetrics {

    private final LongCounter patientReportableCounter;
    private final LongCounter patientNonReportableCounter;
    private final LongCounter measureEvaluatedCounter;
    private final LongCounter recordsReceivedCounter;
    private final LongHistogram evaluationDuration;
    private final LongHistogram normalizedToReportGeneratedDuration;
    private final DoubleHistogram resourceCacheReadDuration;
    private final LongCounter durableCountReadFailureCounter;

    /**
     * Same instrument name and {@code cache.outcome} values as the .NET resource cache
     * (DiagnosticNames.ResourceCacheReadDuration, ResourceCacheOutcomes), so one panel covers
     * every service that reads the cache. {@code cache.fallback.reason} is MeasureEval's own
     * addition: it separates a partial entry rejected by the durable-count check from a plain miss
     * or a Redis outage, which is the distinction the check exists to make visible.
     */
    static final String RESOURCE_CACHE_READ_DURATION = "link_resource_cache_read_duration";
    static final String CACHE_OUTCOME = "cache.outcome";
    static final String CACHE_FALLBACK_REASON = "cache.fallback.reason";

    /**
     * Counts reads where the durable count could not be read, so the cache entry was trusted
     * without the partial-entry check. Occasional is expected; a meaningful share of hits means the
     * check is effectively off.
     */
    static final String DURABLE_COUNT_READ_FAILURE_COUNT = "link_resource_cache_durable_count_read_failure_count";

    public MeasureEvalMetrics(OpenTelemetry openTelemetry) {

        Meter meter = openTelemetry.getMeter("com.lantanagroup.link.measureeval.services.ResourcesNormalizedConsumer");

        patientReportableCounter = meter
                .counterBuilder("link_measureeval_patient_reportable_count")
                .setDescription("The number of patients that were reportable")
                .build();
        patientNonReportableCounter = meter
                .counterBuilder("link_measureeval_patient_not_reportable_count")
                .setDescription("The number of patients that were not reportable")
                .build();
        measureEvaluatedCounter = meter
                .counterBuilder("link_measureeval_eval_count")
                .setDescription("The number of measures evaluated")
                .build();
        recordsReceivedCounter = meter.
                counterBuilder("link_measureeval_records_count")
                .setDescription("The number of records received")
                .build();
        evaluationDuration = meter.histogramBuilder("link_measureeval_eval_duration")
                .ofLongs()
                .setDescription("The duration of the evaluation of a measure")
                .setUnit("ms")
                .setExplicitBucketBoundariesAdvice(HistogramBuckets.DURATION_MS_LONG)
                .build();
        normalizedToReportGeneratedDuration = meter.histogramBuilder("MeasureEval.normalized_to_report_generated.duration")
                .ofLongs()
                .setDescription("End-to-end duration from Kafka Normalized message ingestion to MeasureReportGenerated production")
                .setUnit("ms")
                .setExplicitBucketBoundariesAdvice(HistogramBuckets.DURATION_MS_LONG)
                .build();
        resourceCacheReadDuration = meter.histogramBuilder(RESOURCE_CACHE_READ_DURATION)
                .setDescription("Duration of a resource cache read, tagged by where the resources came from")
                .setUnit("ms")
                .setExplicitBucketBoundariesAdvice(HistogramBuckets.DURATION_MS_DOUBLE)
                .build();
        durableCountReadFailureCounter = meter
                .counterBuilder(DURABLE_COUNT_READ_FAILURE_COUNT)
                .setDescription("Reads that trusted the cache entry because its durable count could not be read")
                .build();
    }

    /**
     * Records one resource cache read.
     *
     * @param outcome        hit, fallback or empty (see {@link ResourceCacheReader})
     * @param fallbackReason why Redis did not serve the read, or {@code null} on a hit
     * @param phase          the pass the read belongs to (Initial or Supplemental), or {@code null}
     *                       when the caller does not know it; the tag is then omitted
     * @param milliseconds   elapsed time of the whole read, including any ABS fallback
     */
    public void recordResourceCacheRead(String outcome, String fallbackReason, String phase, double milliseconds) {
        AttributesBuilder builder = Attributes.builder().put(stringKey(CACHE_OUTCOME), safe(outcome));
        if (fallbackReason != null) {
            builder.put(stringKey(CACHE_FALLBACK_REASON), fallbackReason);
        }
        putPhase(builder, phase);
        resourceCacheReadDuration.record(milliseconds, builder.build());
    }

    /**
     * Counts one read whose durable count could not be read, so the entry was trusted unchecked.
     *
     * @param phase the pass the read belongs to, or {@code null}; the tag is then omitted
     */
    public void incrementDurableCountReadFailure(String phase) {
        AttributesBuilder builder = Attributes.builder();
        putPhase(builder, phase);
        durableCountReadFailureCounter.add(1, builder.build());
    }

    private static void putPhase(AttributesBuilder builder, String phase) {
        if (phase != null) {
            builder.put(stringKey(DiagnosticNames.PHASE), phase);
        }
    }

    public void IncrementPatientReportableCounter(Attributes attributes, boolean reportable) {
        if (reportable) {
            patientReportableCounter.add(1, attributes);
        } else {
            patientNonReportableCounter.add(1, attributes);
        }
    }

    public void IncrementPatientReportableCounter(Attributes attributes) {
        patientReportableCounter.add(1, attributes);
    }

    public void IncrementPatientNonReportableCounter(Attributes attributes) {
        patientNonReportableCounter.add(1, attributes);
    }

    public void IncrementRecordsReceivedCounter(Attributes attributes) {
        recordsReceivedCounter.add(1, attributes);
    }

    void MeasureEvalDuration(long elapsedTime, Attributes attributes) {
        measureEvaluatedCounter.add(1, attributes);
        evaluationDuration.record(elapsedTime, attributes);
    }

    public static Attributes buildAttributes(String queryType,
                                             PatientReportingEvaluationStatus patientStatus,
                                             String reportType) {
        AttributesBuilder builder = Attributes.builder()
                .put(stringKey(DiagnosticNames.FACILITY_ID), safe(patientStatus.getFacilityId()))
                .put(stringKey("report.type"), safe(reportType));
        if (queryType != null) {
            builder.put(stringKey(DiagnosticNames.PHASE), DiagnosticNames.normalizePhase(queryType));
        }
        return builder.build();
    }

    public static Attributes buildPatientOutcomeAttributes(String queryType,
                                                           PatientReportingEvaluationStatus patientStatus) {
        AttributesBuilder builder = Attributes.builder()
                .put(stringKey(DiagnosticNames.FACILITY_ID), safe(patientStatus.getFacilityId()));
        if (queryType != null) {
            builder.put(stringKey(DiagnosticNames.PHASE), DiagnosticNames.normalizePhase(queryType));
        }
        return builder.build();
    }

    void recordNormalizedToReportGeneratedDuration(long elapsedTimeMs, Attributes attributes) {
        normalizedToReportGeneratedDuration.record(elapsedTimeMs, attributes);
    }
}
