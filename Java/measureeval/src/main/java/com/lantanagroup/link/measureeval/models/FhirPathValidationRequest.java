package com.lantanagroup.link.measureeval.models;

import lombok.Getter;
import lombok.Setter;

/**
 * Request to statically validate a FHIRPath expression against a FHIR resource type.
 */
@Getter
@Setter
public class FhirPathValidationRequest {
    /**
     * The FHIR resource type the expression is rooted at (e.g. "Location").
     */
    private String resourceType;

    /**
     * The FHIRPath expression to validate.
     */
    private String fhirPath;
}
