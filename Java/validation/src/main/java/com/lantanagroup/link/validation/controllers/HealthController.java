package com.lantanagroup.link.validation.controllers;

import io.swagger.v3.oas.annotations.Operation;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/health")
public class HealthController {
    @Operation(summary = "Checks service health")
    @GetMapping
    public String checkHealth() {
        return "OK";
    }
}
