package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.model.ResultModel;
import com.lantanagroup.link.validation.services.ValidationService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@RestController
@RequestMapping("/api/validation")
@SecurityRequirement(name = "bearer-key")
public class ValidationController {
    private static final Logger log = LoggerFactory.getLogger(ValidationController.class);

    private final ValidationService validationService;

    public ValidationController(final ValidationService validationService) {
        this.validationService = validationService;
    }

    @Operation(
            summary = "Reload validation artifacts",
            description = "Reload the artifacts from the database into validation so that the next validation will use the latest artifacts",
            tags = {"Validation"},
            operationId = "reloadArtifacts"
    )
    @PostMapping("/reload")
    public void reloadArtifacts() {
        log.info("Reloading artifacts in validation service");
        this.validationService.initArtifacts();
    }

    @Operation(
            summary = "Validate a resource",
            description = "Validate a FHIR resource provided in the request against the FHIR conformance resources loaded in the validation service",
            tags = {"Validation"},
            operationId = "validateResource"
    )
    @PostMapping("/validate")
    public OperationOutcome validate(@RequestBody Bundle bundle) {
        log.info("Validating bundle with ID {}", bundle.hasId() ? bundle.getIdElement().getIdPart() : "UNKNOWN");
        List<ResultModel> results = this.validationService.validate(bundle);
        return this.validationService.convertToOperationOutcome(results);
    }
}
