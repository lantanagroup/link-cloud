package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.shared.auth.JwtService;
import com.lantanagroup.link.shared.services.ReportClient;
import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.client.RestClient;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

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

    /**
     * How many bundle chunks validate at once. Memory is bounded by this times one
     * in-flight HAPI validation (entries inside a chunk run sequentially).
     */
    @Getter @Setter
    private int bundleValidationParallelism = 4;

    /**
     * Entries per chunk. Each chunk is one pool task; HAPI never sees the full bundle.
     */
    @Getter @Setter
    private int bundleValidationBatchSize = 32;

    /**
     * Configured validation-result rules whose matches should be dropped before categorization,
     * persistence, and downstream validity calculations.
     */
    @Getter @Setter
    private List<ValidationResultIgnoreRuleConfig> validationResultIgnoreRules = new ArrayList<>();

    @Bean(name = "bundleValidationExecutor", destroyMethod = "shutdown")
    public ExecutorService bundleValidationExecutor() {
        int parallelism = Math.max(1, bundleValidationParallelism);
        return Executors.newFixedThreadPool(parallelism, runnable -> {
            Thread thread = new Thread(runnable, "hapi-bundle-validation");
            thread.setDaemon(true);
            return thread;
        });
    }
}
