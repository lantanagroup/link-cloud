package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.validation.services.ReportClient;
import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.client.RestClient;

@Configuration
@ConfigurationProperties("link")
public class LinkConfig {
    @Bean
    @ConfigurationProperties("report")
    public ReportClient reportClient(RestClient restClient) {
        return new ReportClient(restClient);
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
}
