package com.lantanagroup.link.measureeval.controllers;

import io.swagger.v3.oas.annotations.Operation;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/health")
public class HealthController {
    @Operation(summary = "Health check", description = "Health check")
    @GetMapping
    public String get() {
        return "OK";
    }
}
