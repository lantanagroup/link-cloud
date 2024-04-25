package com.lantanagroup.link.measureeval.serdes;

import ca.uhn.fhir.context.FhirContext;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;
import org.springframework.stereotype.Component;

import java.util.List;

@Component
public class FhirMongoCustomConversions extends MongoCustomConversions {
    public FhirMongoCustomConversions(FhirContext fhirContext) {
        super(List.of(
                new FhirResourceReader(fhirContext),
                new FhirResourceWriter(fhirContext)));
    }
}
