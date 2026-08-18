package com.lantanagroup.link.validation.configs;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

/**
 * Flags controlling the pre-qualification OperationOutcome that the Validation service writes to the
 * patient NDJSON. These are the Java-runtime keys for a cross-runtime concept; the .NET Report service
 * reads the equivalent {@code PreQualification:WritePreQualOperationOutcome} key (LEGLINK-466).
 */
@Getter
@Setter
@Configuration
@ConfigurationProperties("pre-qualification")
public class PreQualificationConfig {
    /**
     * Master gate. When true, the Validation service is the sole writer of the pre-qualification
     * OperationOutcome to the patient NDJSON. When false (default), it writes nothing.
     */
    private boolean writePreQualOperationOutcome = false;

    /**
     * When false, the {@code expression[]} of each issue is omitted from the OperationOutcome.
     */
    private boolean writeExpressionsInOperationOutcome = true;
}
