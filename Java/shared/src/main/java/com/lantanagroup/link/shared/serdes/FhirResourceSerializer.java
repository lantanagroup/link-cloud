package com.lantanagroup.link.shared.serdes;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.core.JsonGenerator;
import com.fasterxml.jackson.databind.SerializerProvider;
import com.fasterxml.jackson.databind.ser.std.StdSerializer;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.io.IOException;

public class FhirResourceSerializer<T extends IBaseResource> extends StdSerializer<T> {
    private final FhirContext fhirContext;

    public FhirResourceSerializer(Class<T> resourceType, FhirContext fhirContext) {
        super(resourceType);
        this.fhirContext = fhirContext;
    }

    @Override
    public void serialize(T value, JsonGenerator jsonGenerator, SerializerProvider serializerProvider)
            throws IOException {
        String json = fhirContext.newJsonParser().encodeResourceToString(value);
        jsonGenerator.writeRawValue(json);
    }
}
