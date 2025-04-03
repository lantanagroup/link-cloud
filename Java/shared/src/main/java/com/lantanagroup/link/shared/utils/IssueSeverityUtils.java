package com.lantanagroup.link.shared.utils;

import org.hl7.fhir.r4.model.OperationOutcome;

public class IssueSeverityUtils {
    public static boolean isAsSevere(
            OperationOutcome.IssueSeverity severity,
            OperationOutcome.IssueSeverity threshold) {
        if (severity == null || severity == OperationOutcome.IssueSeverity.NULL) {
            throw new IllegalArgumentException("Severity is null");
        }
        if (threshold == null || threshold == OperationOutcome.IssueSeverity.NULL) {
            throw new IllegalArgumentException("Threshold is null");
        }
        return severity.ordinal() <= threshold.ordinal();
    }
}
