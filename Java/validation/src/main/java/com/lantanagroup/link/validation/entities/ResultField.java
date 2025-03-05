package com.lantanagroup.link.validation.entities;

import org.hl7.fhir.r4.model.OperationOutcome;

import java.util.Optional;

public enum ResultField {
    SEVERITY,
    CODE,
    MESSAGE,
    EXPRESSION;

    public String getValue(Result result) {
        return switch (this) {
            case SEVERITY -> Optional.ofNullable(result.getSeverity())
                    .map(OperationOutcome.IssueSeverity::toCode)
                    .orElse(null);
            case CODE -> Optional.ofNullable(result.getCode())
                    .map(OperationOutcome.IssueType::toCode)
                    .orElse(null);
            case MESSAGE -> result.getMessage();
            case EXPRESSION -> result.getExpression();
        };
    }
}
