package com.lantanagroup.link.validation.controllers;

import io.swagger.v3.oas.annotations.Operation;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/_health")
public class HealthController {
    @Operation(
            summary = "Health check",
            description = "Health check endpoint for the validation service",
            tags = {"Health"},
            operationId = "health"
    )
    @GetMapping
    public String health() {
        return "OK";
    }
}
