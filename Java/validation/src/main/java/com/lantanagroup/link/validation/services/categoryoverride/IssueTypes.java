package com.lantanagroup.link.validation.services.categoryoverride;

import lombok.extern.slf4j.Slf4j;
import org.hl7.fhir.r4.model.OperationOutcome;

import java.util.Map;

@Slf4j
public final class IssueTypes {

    /**
     * Rubric-specific codes that have an unambiguous FHIR IssueType equivalent. Without these the
     * one category rule in categories.json that matches on {@code CODE}
     * ({@code invalid_code_in_required_valueset}, which requires {@code code-invalid}) could never
     * match a rubric finding, since no executor emits a FHIR code.
     */
    private static final Map<String, OperationOutcome.IssueType> ALIASES = Map.of(
            "terminology-code-invalid", OperationOutcome.IssueType.CODEINVALID,
            "valueset-membership-failed", OperationOutcome.IssueType.CODEINVALID,
            "fhirpath-evaluation-error", OperationOutcome.IssueType.PROCESSING,
            "check-execution-error", OperationOutcome.IssueType.PROCESSING,
            "custom-check-error", OperationOutcome.IssueType.PROCESSING,
            "custom-check-not-found", OperationOutcome.IssueType.NOTFOUND);

    private IssueTypes() {
    }

    public static OperationOutcome.IssueType parseOrNull(String code) {
        if (code == null || code.isBlank()) {
            return null;
        }
        OperationOutcome.IssueType alias = ALIASES.get(code);
        if (alias != null) {
            return alias;
        }
        try {
            return OperationOutcome.IssueType.fromCode(code);
        } catch (Exception e) {
            // Expected for every rubric-native code; category rules keyed on MESSAGE or EXPRESSION
            // still match fine with a null code.
            log.trace("Finding code '{}' is not a FHIR IssueType; matching on it will be skipped", code);
            return null;
        }
    }
}
