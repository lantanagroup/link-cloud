package com.lantanagroup.link.measureeval.config;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.serdes.FhirMongoCustomConversions;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class MongoConfig {
    @Bean
    public FhirMongoCustomConversions fhirCustomConversions(FhirContext fhirContext) {
        return new FhirMongoCustomConversions(fhirContext);
    }
}
