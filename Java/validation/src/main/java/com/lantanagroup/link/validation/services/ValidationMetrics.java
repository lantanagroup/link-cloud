package com.lantanagroup.link.validation.services;

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
    private final DoubleHistogram validationDuration;
    private final DoubleHistogram categorizationDuration;

    public ValidationMetrics(OpenTelemetry openTelemetry) {
        Meter meter = openTelemetry.getMeter(ValidationMetrics.class.getName());
        validationCounter = meter.counterBuilder("link.validation.counter").build();
        validationDuration = meter.histogramBuilder("link.validation.validate.duration")
                .setDescription("The duration of the validation process, excluding persisting validation results")
                .setUnit("ms")
                .build();
        categorizationDuration = meter.histogramBuilder("link.validation.categorization.duration")
                .setDescription("The duration of the categorization process, excluding persisting categorized results")
                .setUnit("ms")
                .build();
    }

    public void addToValidationCounter(Attributes attributes) {
        validationCounter.add(1L, attributes);
    }

    public void recordValidationDuration(double millis, Attributes attributes) {
        validationDuration.record(millis, attributes);
    }

    public void recordCategorizationDuration(double millis, Attributes attributes) {
        categorizationDuration.record(millis, attributes);
    }
}
