package com.lantanagroup.link.shared.mongo;

import com.lantanagroup.link.shared.fhir.FhirHelper;
import org.bson.Document;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.core.convert.converter.Converter;

public class BundleSerializer implements Converter<Bundle, Document> {
    @Override
    public Document convert(Bundle source) {
        String json = FhirHelper.serialize(source);
        return Document.parse(json);
    }
}
