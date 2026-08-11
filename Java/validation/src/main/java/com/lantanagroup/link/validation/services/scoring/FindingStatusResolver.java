package com.lantanagroup.link.validation.services.scoring;

import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;
import org.springframework.stereotype.Component;

@Component
public class FindingStatusResolver {

    public RubricResultStatus statusOf(EvaluatedFinding finding) {
        Boolean acceptable = finding.acceptable();
        if (Boolean.FALSE.equals(acceptable)) {
            return RubricResultStatus.UNACCEPTABLE;
        }
        if (Boolean.TRUE.equals(acceptable)) {
            return atLeastWarning(finding.effectiveSeverity())
                    ? RubricResultStatus.ACCEPTABLE_WITH_WARNINGS
                    : RubricResultStatus.ACCEPTABLE;
        }
        return bySeverityAlone(finding.effectiveSeverity());
    }

    /** The pre-override mapping, unchanged: an uncategorized error still fails. */
    private RubricResultStatus bySeverityAlone(Severity severity) {
        if (severity == null) {
            return RubricResultStatus.ACCEPTABLE;
        }
        return switch (severity) {
            case ERROR -> RubricResultStatus.UNACCEPTABLE;
            case WARNING -> RubricResultStatus.ACCEPTABLE_WITH_WARNINGS;
            case INFORMATION -> RubricResultStatus.ACCEPTABLE;
        };
    }

    private boolean atLeastWarning(Severity severity) {
        return severity == Severity.WARNING || severity == Severity.ERROR;
    }
}
