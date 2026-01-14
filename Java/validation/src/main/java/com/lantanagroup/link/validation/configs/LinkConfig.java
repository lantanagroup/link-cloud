package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.shared.auth.JwtService;
import com.lantanagroup.link.validation.services.ReportClient;
import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.client.RestClient;

import java.util.ArrayList;
import java.util.List;

@Configuration
@ConfigurationProperties("link")
public class LinkConfig {
    @Bean
    @ConfigurationProperties("link.report")
    public ReportClient reportClient(JwtService jwtService, RestClient restClient) {
        return new ReportClient(jwtService, restClient);
    }

    /**
     * The root URL of the Link terminology service.
     */
    @Getter @Setter
    private String terminologyServiceUrl;

    /**
     * The root URL of a FHIR terminology service; to use in place of the Link terminology service.
     */
    @Getter @Setter
    private String fhirTerminologyServiceUrl;

    @Getter @Setter
    private List<String> whiteListCodeSystemRegex = new ArrayList<>();

    @Getter @Setter
    private List<String> whiteListValueSetRegex = new ArrayList<>();
}
