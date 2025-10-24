package com.lantanagroup.link.validation.services;

import io.opentelemetry.api.metrics.LongCounter;
import io.opentelemetry.api.metrics.LongUpDownCounter;
import io.opentelemetry.api.metrics.Meter;
import lombok.Getter;
import org.springframework.stereotype.Service;

@Service
public class MetricService {
    private static final String ValidationDurationMetricName = "validation_dur";
    private static final String ValidationResultsCountMetricName = "validation_results_count";
    private static final String CategorizationDurationMetricName = "categorization_dur";

    @Getter
    private final LongCounter validationResultsCounter;

    @Getter
    private final LongUpDownCounter validationDurationUpDown;

    @Getter
    private final LongUpDownCounter categorizationDurationUpDown;

    public MetricService(Meter meter) {
        this.validationResultsCounter = meter.counterBuilder(ValidationResultsCountMetricName)
                .setDescription("Number of validation results")
                .build();
        this.validationDurationUpDown = meter.upDownCounterBuilder(ValidationDurationMetricName)
                .setDescription("Time taken to validate resource")
                .setUnit("s")
                .build();
        this.categorizationDurationUpDown = meter.upDownCounterBuilder(CategorizationDurationMetricName)
                .setDescription("Time take to categorize validation results")
                .setUnit("s")
                .build();
    }
}
