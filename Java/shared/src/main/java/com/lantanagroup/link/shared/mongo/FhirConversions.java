package com.lantanagroup.link.shared.mongo;

import org.hl7.fhir.r4.model.Bundle;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;

import java.util.List;

public class FhirConversions extends MongoCustomConversions {
    public FhirConversions() {
        super(List.of(new BundleDeserializer(), new BundleSerializer()));
    }
}
