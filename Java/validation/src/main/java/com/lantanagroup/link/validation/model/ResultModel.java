package com.lantanagroup.link.validation.model;

import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.OperationOutcome;

@Getter
@Setter
public class ResultModel {
    private String message;

    private String expression;

    private OperationOutcome.IssueSeverity severity;

    private OperationOutcome.IssueType type;

    private String location;
}
