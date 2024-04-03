package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.services.ValidationService;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api")
public class ValidationController {
    private final ValidationService validationService;

    public ValidationController(final ValidationService validationService) {
        this.validationService = validationService;
    }

    @GetMapping
    public String getValidationResults() {
        return this.validationService.getValidationResults();
    }
}
