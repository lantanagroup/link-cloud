package com.lantanagroup.link.measureeval.controllers;

import com.lantanagroup.link.measureeval.models.FhirPathValidationRequest;
import com.lantanagroup.link.measureeval.models.FhirPathValidationResponse;
import com.lantanagroup.link.measureeval.services.FhirPathValidationService;
import io.swagger.v3.oas.annotations.Operation;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/measureeval/fhir-path")
@PreAuthorize("hasAnyRole('LinkUser', 'LinkAdministrator')")
public class FhirPathController {

    private final FhirPathValidationService validationService;

    public FhirPathController(FhirPathValidationService validationService) {
        this.validationService = validationService;
    }

    /**
     * Statically validates a FHIRPath expression against a resource type. Returns 200 with a
     * structured result (valid + errors + warnings) in all cases — a malformed expression is a
     * successful validation that found problems, not an HTTP error — so callers can surface the
     * findings inline.
     */
    @PostMapping("/$validate")
    @Operation(summary = "Validate a FHIRPath expression against a FHIR resource type",
            tags = {"FHIRPath"})
    public FhirPathValidationResponse validate(@RequestBody FhirPathValidationRequest request) {
        return validationService.validate(request.getResourceType(), request.getFhirPath());
    }
}
