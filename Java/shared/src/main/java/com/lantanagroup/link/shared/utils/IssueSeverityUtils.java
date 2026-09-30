package com.lantanagroup.link.shared.utils;

import org.hl7.fhir.r4.model.OperationOutcome;

import java.util.EnumSet;
import java.util.Set;

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

    /**
     * Severities that {@link #isAsSevere} accepts for {@code threshold}, excluding {@code NULL}.
     * Stored result severities use these enum names.
     */
    public static Set<OperationOutcome.IssueSeverity> atLeastAsSevere(OperationOutcome.IssueSeverity threshold) {
        if (threshold == null || threshold == OperationOutcome.IssueSeverity.NULL) {
            throw new IllegalArgumentException("Threshold is null");
        }
        EnumSet<OperationOutcome.IssueSeverity> severities = EnumSet.noneOf(OperationOutcome.IssueSeverity.class);
        for (OperationOutcome.IssueSeverity severity : OperationOutcome.IssueSeverity.values()) {
            if (severity != OperationOutcome.IssueSeverity.NULL && isAsSevere(severity, threshold)) {
                severities.add(severity);
            }
        }
        return severities;
    }
}
