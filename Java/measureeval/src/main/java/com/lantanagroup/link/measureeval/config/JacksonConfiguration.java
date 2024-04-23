package com.lantanagroup.link.measureeval.config;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.serdes.FhirAwareModule;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class JacksonConfiguration {
    @Bean
    public FhirAwareModule fhirAwareModule(FhirContext fhirContext) {
        return new FhirAwareModule(fhirContext);
    }
}
