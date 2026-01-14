package com.lantanagroup.link.measureeval.configs;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.Module;
import com.lantanagroup.link.shared.serdes.FhirModule;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class JacksonConfig {
    @Bean
    public Module fhirModule(FhirContext fhirContext) {
        return new FhirModule(fhirContext);
    }
}
