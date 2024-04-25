package com.lantanagroup.link.shared.mongo;

import com.lantanagroup.link.shared.fhir.FhirHelper;
import org.bson.Document;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.core.convert.converter.Converter;

public class BundleDeserializer implements Converter<Document, Bundle> {
    @Override
    public Bundle convert(Document source) {
        String json = source.toJson();
        return (Bundle) FhirHelper.deserialize(json);
    }
}
