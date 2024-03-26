package com.lantanagroup.link.measureeval.controllers;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/_health")
public class HealthController {
    @GetMapping
    public String health() {
        return "OK";
    }
}
