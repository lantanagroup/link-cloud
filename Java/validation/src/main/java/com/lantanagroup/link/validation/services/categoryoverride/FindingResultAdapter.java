package com.lantanagroup.link.validation.services.categoryoverride;

import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RawFinding;
import org.hl7.fhir.r4.model.OperationOutcome;

public final class FindingResultAdapter {

    private FindingResultAdapter() {
    }

    public static Result toTransientResult(RawFinding finding) {
        return toResult(finding.getSeverity(), finding.getCode(), finding.getMessage(),
                finding.getLocation(), finding.getExpression());
    }

    /**
     * Shared severity/code/message/location/expression mapping for anything shaped like a rubric
     * finding (currently {@code RawFinding} and, via the ADR-0003 bridge, {@code FindingDto}).
     */
    public static Result toResult(Severity severity, String code, String message, String location, String expression) {
        Result result = new Result();
        result.setSeverity(toIssueSeverity(severity));
        result.setCode(IssueTypes.parseOrNull(code));
        result.setMessage(message);
        result.setLocation(location);
        result.setExpression(expression);
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
