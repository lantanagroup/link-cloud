package com.lantanagroup.link.measureeval.configs;

import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.MeasureReport;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.util.function.Predicate;

@Getter
@Setter
@Configuration
@ConfigurationProperties("link")
public class LinkConfig {
    private String reportabilityPredicate;

    /**
     * The maximum number of threads to use to collect resources from the mongo database when bundling a patient's resources.
     */
    private Integer maxCollectResourcesThreads;

    private boolean cqlDebug = false;

    @Bean
    @SuppressWarnings("unchecked")
    public Predicate<MeasureReport> reportabilityPredicate() throws Exception {
        return (Predicate<MeasureReport>) Class.forName(reportabilityPredicate).getConstructor().newInstance();
    }

}
