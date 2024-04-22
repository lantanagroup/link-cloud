package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.services.ValidationService;
import io.swagger.v3.oas.annotations.Operation;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/validation")
public class ValidationController {
    private static final Logger log = LoggerFactory.getLogger(ValidationController.class);

    private final ValidationService validationService;

    public ValidationController(final ValidationService validationService) {
        this.validationService = validationService;
    }

    @Operation(
            summary = "Reload validation artifacts",
            description = "Reload the artifacts from the database into validation so that the next validation will use the latest artifacts"
    )
    @PostMapping("/reload")
    public void reloadArtifacts() {
        log.info("Reloading artifacts in validation service");
        this.validationService.initialize();
    }

    @GetMapping
    public String getValidationResults() {
        return "test";
    }
}
