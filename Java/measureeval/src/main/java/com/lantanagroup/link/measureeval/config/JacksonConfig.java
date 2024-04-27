package com.lantanagroup.link.measureeval.config;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.serdes.FhirModule;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class JacksonConfig {
    @Bean
    public FhirModule fhirModule(FhirContext fhirContext) {
        return new FhirModule(fhirContext);
    }
}
