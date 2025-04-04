package com.lantanagroup.link.measureeval.configs;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.shared.serdes.FhirMongoCustomConversions;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;

@Configuration
public class MongoConfig {
    @Bean
    public MongoCustomConversions fhirCustomConversions(FhirContext fhirContext) {
        return new FhirMongoCustomConversions(fhirContext);
    }
}
