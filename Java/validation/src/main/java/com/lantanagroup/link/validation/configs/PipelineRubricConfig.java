package com.lantanagroup.link.validation.configs;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

/**
 * The rubric the reporting pipeline validates against, as the rubric registry needs it: retiring that
 * rubric's last published version while the pipeline decides on it would leave every ReadyForValidation
 * with nothing to run, so the registry refuses it. The same keys are read by the Kafka consumers.
 */
@Getter
@Setter
@Configuration
@ConfigurationProperties("vaas.bridge")
public class PipelineRubricConfig {
    /** Whether the pipeline decides on the rubric engine's result ({@code vaas.bridge.enabled}). */
    private boolean enabled = false;

    /** The rubric the pipeline evaluates ({@code vaas.bridge.rubric-id}). */
    private String rubricId = "measure-report-submission-v1";

    /** True when the pipeline decides on {@code rubricId}'s newest published version. */
    public boolean decidesOn(String rubricId) {
        return enabled && this.rubricId != null && this.rubricId.equals(rubricId);
    }
}
