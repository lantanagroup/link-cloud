package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.shared.auth.JwtService;
import com.lantanagroup.link.validation.services.ReportClient;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.client.RestClient;

@Configuration
public class LinkConfig {
    @Bean
    @ConfigurationProperties("link.report")
    public ReportClient reportClient(JwtService jwtService, RestClient restClient) {
        return new ReportClient(jwtService, restClient);
    }
}
