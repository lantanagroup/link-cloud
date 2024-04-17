package com.lantanagroup.link.measureeval.serdes;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.JsonDeserializer;
import jakarta.annotation.Nonnull;
import org.bson.Document;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.core.convert.converter.Converter;

import java.io.IOException;
import java.lang.reflect.Modifier;

public class FhirResourceDeserializer<T extends IBaseResource>
        extends JsonDeserializer<T>
        implements Converter<Document, T> {
    private final Class<T> resourceType;
    private final FhirContext fhirContext;

    public FhirResourceDeserializer(Class<T> resourceType, FhirContext fhirContext) {
        this.resourceType = resourceType;
        this.fhirContext = fhirContext;
    }

    private T parse(String json) {
        Class<T> parseType = Modifier.isAbstract(resourceType.getModifiers()) ? null : resourceType;
        return fhirContext.newJsonParser().parseResource(parseType, json);
    }

    @Override
    public T deserialize(JsonParser jsonParser, DeserializationContext deserializationContext) throws IOException {
        return parse(jsonParser.readValueAsTree().toString());
    }

    @Override
    public T convert(@Nonnull Document source) {
        return parse(source.toJson());
    }
}
