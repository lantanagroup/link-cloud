package com.lantanagroup.link.measureeval.config;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.measureeval.serdes.FhirAwareModule;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.support.JacksonUtils;

@Configuration
public class JacksonConfiguration {
    @Bean
    public FhirAwareModule fhirAwareModule(FhirContext fhirContext) {
        return new FhirAwareModule(fhirContext);
    }

    @Bean
    public ObjectMapper fhirAwareObjectMapper(FhirAwareModule fhirAwareModule) {
        ObjectMapper objectMapper = JacksonUtils.enhancedObjectMapper();
        objectMapper.registerModule(fhirAwareModule);
        return objectMapper;
    }
}
