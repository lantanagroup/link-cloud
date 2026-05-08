package com.lantanagroup.link.measureeval.controllers;

import com.lantanagroup.link.shared.config.ServiceInformationConfig;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
public class ApiInfoController {
    private final ServiceInformationConfig serviceInformationConfig;

    public ApiInfoController(ServiceInformationConfig serviceInformationConfig) {
        this.serviceInformationConfig = serviceInformationConfig;
    }

    @GetMapping("${link.info-route:/api/info}")
    public ServiceInformationConfig info() {
        return this.serviceInformationConfig;
    }
}
