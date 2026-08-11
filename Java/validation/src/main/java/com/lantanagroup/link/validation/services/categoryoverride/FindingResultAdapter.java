package com.lantanagroup.link.validation.services.categoryoverride;

import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RawFinding;
import org.hl7.fhir.r4.model.OperationOutcome;

public final class FindingResultAdapter {

    private FindingResultAdapter() {
    }

    public static Result toTransientResult(RawFinding finding) {
        Result result = new Result();
        result.setSeverity(toIssueSeverity(finding.getSeverity()));
        result.setCode(IssueTypes.parseOrNull(finding.getCode()));
        result.setMessage(finding.getMessage());
        result.setLocation(finding.getLocation());
        result.setExpression(finding.getExpression());
        return result;
    }

    /**
     * Exhaustive mapping, deliberately not {@code IssueSeverity.fromCode}: adding a Severity value
     * becomes a compile error here rather than a runtime throw. Rubric findings cannot be FATAL.
     */
    private static OperationOutcome.IssueSeverity toIssueSeverity(Severity severity) {
        if (severity == null) {
            return null;
        }
        return switch (severity) {
            case ERROR -> OperationOutcome.IssueSeverity.ERROR;
            case WARNING -> OperationOutcome.IssueSeverity.WARNING;
            case INFORMATION -> OperationOutcome.IssueSeverity.INFORMATION;
        };
    }
}
