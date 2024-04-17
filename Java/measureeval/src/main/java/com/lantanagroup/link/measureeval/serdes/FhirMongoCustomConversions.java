package com.lantanagroup.link.measureeval.serdes;

import ca.uhn.fhir.context.FhirContext;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;

public class FhirMongoCustomConversions extends MongoCustomConversions {
    public FhirMongoCustomConversions(FhirContext fhirContext) {
        super(new FhirMongoConverterConfigurationAdapter(fhirContext));
    }
}
