package com.lantanagroup.link.shared.serdes;

import ca.uhn.fhir.context.FhirContext;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;

import java.util.List;

public class FhirMongoCustomConversions extends MongoCustomConversions {
    public FhirMongoCustomConversions(FhirContext fhirContext) {
        super(List.of(
                new FhirResourceReader(fhirContext),
                new FhirResourceWriter(fhirContext)));
    }
}
