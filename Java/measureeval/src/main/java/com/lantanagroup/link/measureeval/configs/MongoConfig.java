package com.lantanagroup.link.measureeval.configs;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.shared.serdes.FhirResourceReader;
import com.lantanagroup.link.shared.serdes.FhirResourceWriter;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;

import java.util.List;

@Configuration
public class MongoConfig {
    @Bean
    public MongoCustomConversions fhirCustomConversions(FhirContext fhirContext) {
        return new MongoCustomConversions(List.of(
                new FhirResourceReader(fhirContext),
                new FhirResourceWriter(fhirContext),
                new DocumentToStringConverter()));
    }
}
