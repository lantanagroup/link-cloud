package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.utils.HistogramBuckets;
import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.metrics.DoubleHistogram;
import io.opentelemetry.api.metrics.LongCounter;
import io.opentelemetry.api.metrics.LongHistogram;
import io.opentelemetry.api.metrics.Meter;
import org.springframework.stereotype.Service;

@Service
public class ValidationMetrics {
    private final LongCounter validationCounter;
    private final LongCounter validationIssuesCounter;
    private final DoubleHistogram validationDuration;
    private final DoubleHistogram categorizationDuration;
    private final DoubleHistogram reportFetchDuration;

    public ValidationMetrics(OpenTelemetry openTelemetry) {
        Meter meter = openTelemetry.getMeter(ValidationMetrics.class.getName());
        validationCounter = meter.counterBuilder("link.validation.counter").build();
        validationIssuesCounter = meter.counterBuilder("link.validation.issues")
                .setDescription("Validation issue count by severity")
                .build();
        validationDuration = meter.histogramBuilder("link.validation.validate.duration")
                .setDescription("The duration of the validation process, excluding persisting validation results")
                .setUnit("ms")
                .setExplicitBucketBoundariesAdvice(HistogramBuckets.DURATION_MS_DOUBLE)
                .build();
        categorizationDuration = meter.histogramBuilder("link.validation.categorization.duration")
                .setDescription("The duration of the categorization process, excluding persisting categorized results")
                .setUnit("ms")
                .setExplicitBucketBoundariesAdvice(HistogramBuckets.DURATION_MS_DOUBLE)
                .build();
        reportFetchDuration = meter.histogramBuilder("link.validation.report_fetch_duration")
                .setDescription("Duration of fetching the patient report bundle (blob or Report HTTP)")
                .setUnit("ms")
                .setExplicitBucketBoundariesAdvice(HistogramBuckets.DURATION_MS_DOUBLE)
                .build();
    }

    public void addToValidationCounter(Attributes attributes) {
        validationCounter.add(1L, attributes);
    }

    public void addIssues(String severity, long count, Attributes baseAttributes) {
        if (count <= 0) {
            return;
        }
        Attributes attributes = baseAttributes.toBuilder()
                .put("severity", severity)
                .build();
        validationIssuesCounter.add(count, attributes);
    }

    public void recordValidationDuration(double millis, Attributes attributes) {
        validationDuration.record(millis, attributes);
    }

    public void recordCategorizationDuration(double millis, Attributes attributes) {
        categorizationDuration.record(millis, attributes);
    }

    public void recordReportFetchDuration(double millis, Attributes attributes) {
        reportFetchDuration.record(millis, attributes);
    }
}
