package com.lantanagroup.link.measureeval.serdes;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.core.JsonGenerator;
import com.fasterxml.jackson.databind.JsonSerializer;
import com.fasterxml.jackson.databind.SerializerProvider;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.io.IOException;

public class FhirResourceSerializer<T extends IBaseResource> extends JsonSerializer<T> {
    private final FhirContext fhirContext;

    public FhirResourceSerializer(FhirContext fhirContext) {
        this.fhirContext = fhirContext;
    }

    @Override
    public void serialize(T value, JsonGenerator jsonGenerator, SerializerProvider serializerProvider)
            throws IOException {
        String json = fhirContext.newJsonParser().encodeResourceToString(value);
        jsonGenerator.writeRawValue(json);
    }
}
