package com.lantanagroup.link.validation.configs;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

/**
 * Dry-run gate on rubric publish. The $dry-run endpoint records outcomes on the version,
 * this flag just controls whether $publish requires one.
 */
@Getter
@Setter
@Configuration
@ConfigurationProperties("link.rubric.dry-run")
public class RubricDryRunConfig {
    /**
     * When true, $publish is blocked unless a dry run was completed for the version with
     * status ACCEPTABLE or ACCEPTABLE_WITH_WARNINGS. When false (default), no check.
     */
    private boolean requiredForPublish = false;
}
