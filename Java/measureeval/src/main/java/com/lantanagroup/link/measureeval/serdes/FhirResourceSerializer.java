package com.lantanagroup.link.measureeval.serdes;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.core.JsonGenerator;
import com.fasterxml.jackson.databind.JsonSerializer;
import com.fasterxml.jackson.databind.SerializerProvider;
import jakarta.annotation.Nonnull;
import org.bson.Document;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.core.convert.converter.Converter;

import java.io.IOException;

public class FhirResourceSerializer<T extends IBaseResource>
        extends JsonSerializer<T>
        implements Converter<T, Document> {
    private final Class<T> resourceType;
    private final FhirContext fhirContext;

    public FhirResourceSerializer(Class<T> resourceType, FhirContext fhirContext) {
        this.resourceType = resourceType;
        this.fhirContext = fhirContext;
    }

    private String encode(T resource) {
        return fhirContext.newJsonParser().encodeToString(resource);
    }

    @Override
    public void serialize(T value, JsonGenerator jsonGenerator, SerializerProvider serializerProvider)
            throws IOException {
        jsonGenerator.writeRawValue(encode(value));
    }

    @Override
    public Document convert(@Nonnull T source) {
        return Document.parse(encode(source));
    }
}
