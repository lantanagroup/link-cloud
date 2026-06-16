package com.lantanagroup.link.validation.services;

import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.api.common.AttributeKey;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.metrics.DoubleHistogram;
import io.opentelemetry.api.metrics.LongCounter;
import io.opentelemetry.api.metrics.LongHistogram;
import io.opentelemetry.api.metrics.Meter;
import org.springframework.stereotype.Service;

@Service
public class ValidationMetrics {
    public static final String OUTCOME_SKIPPED = "skipped";
    public static final String OUTCOME_SUPPRESSED = "suppressed";
    public static final String OUTCOME_LABELED = "labeled";

    private static final AttributeKey<String> ATTR_RULE_ID = AttributeKey.stringKey("rule_id");
    private static final AttributeKey<String> ATTR_OUTCOME = AttributeKey.stringKey("outcome");

    private final LongCounter validationCounter;
    private final DoubleHistogram validationDuration;
    private final DoubleHistogram categorizationDuration;
    private final LongCounter ruleOutcomeCounter;

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
        ruleOutcomeCounter = meter.counterBuilder("link.validation.rule.outcome")
                .setDescription("Counts each time a categorization rule fires, tagged by rule_id and outcome " +
                        "(skipped: rule short-circuited validation via the policy advisor; " +
                        "suppressed: rule dropped the message after the check ran; " +
                        "labeled: rule matched a produced message via post-validation categorization).")
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

    /**
     * Records that a categorization rule fired at the given lifecycle stage. {@code outcome}
     * should be one of {@link #OUTCOME_SKIPPED}, {@link #OUTCOME_SUPPRESSED}, or
     * {@link #OUTCOME_LABELED}.
     */
    public void incrementRuleOutcome(String ruleId, String outcome) {
        ruleOutcomeCounter.add(1L, Attributes.of(
                ATTR_RULE_ID, ruleId,
                ATTR_OUTCOME, outcome));
    }
}
