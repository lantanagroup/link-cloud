package com.lantanagroup.link.validation.services;

import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.metrics.LongCounter;
import io.opentelemetry.api.metrics.LongHistogram;
import io.opentelemetry.api.metrics.Meter;
import org.springframework.stereotype.Service;

@Service
public class ValidationMetrics {
    private final LongCounter validationCounter;
    private final LongHistogram validationDuration;

    public ValidationMetrics(OpenTelemetry openTelemetry) {
        Meter meter = openTelemetry.getMeter(ValidationMetrics.class.getName());
        validationCounter = meter.counterBuilder("ValidationCounter").build();
        validationDuration = meter.histogramBuilder("ValidationDuration")
                .ofLongs()
                .setUnit("ms")
                .build();
    }

    public void addToValidationCounter(Attributes attributes) {
        validationCounter.add(1L, attributes);
    }

    public void recordValidationDuration(long millis, Attributes attributes) {
        validationDuration.record(millis, attributes);
    }
}
