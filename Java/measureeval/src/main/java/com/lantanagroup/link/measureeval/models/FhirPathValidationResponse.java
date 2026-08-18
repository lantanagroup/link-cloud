package com.lantanagroup.link.measureeval.models;

import lombok.Getter;
import lombok.Setter;

import java.util.ArrayList;
import java.util.List;

/**
 * Result of statically validating a FHIRPath expression. {@code valid} is false when the
 * expression has syntax errors or references elements/types that do not exist on the
 * resource type. {@code warnings} carries non-fatal findings (e.g. a non-boolean return
 * type for an expression that is expected to be a boolean condition).
 */
@Getter
@Setter
public class FhirPathValidationResponse {
    private boolean valid;

    /**
     * The inferred return type(s) of the expression, for diagnostics. Null when the
     * expression could not be checked.
     */
    private String returnType;

    private List<String> errors = new ArrayList<>();

    private List<String> warnings = new ArrayList<>();
}
