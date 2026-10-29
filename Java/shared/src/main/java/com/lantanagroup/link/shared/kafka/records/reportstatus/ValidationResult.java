package com.lantanagroup.link.shared.kafka.records.reportstatus;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

/** Validation outcome retained independently of subsequent reporting stages. */
@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class ValidationResult {
    private boolean passed;
    /** Total validation messages, including all categories and severities. */
    private long messageCount;
}
