package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.shared.config.ServiceInformationConfig;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/validation")
public class ApiInfoController {
    private final ServiceInformationConfig serviceInformationConfig;

    public ApiInfoController(ServiceInformationConfig serviceInformationConfig) {
        this.serviceInformationConfig = serviceInformationConfig;
    }

    @RequestMapping("/info")
    public ServiceInformationConfig info() {
        return this.serviceInformationConfig;
    }
}
