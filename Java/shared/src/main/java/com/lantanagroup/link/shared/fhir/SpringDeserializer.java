package com.lantanagroup.link.shared.fhir;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.deser.std.StdDeserializer;
import org.hl7.fhir.r4.model.Resource;

import java.io.IOException;

public class SpringDeserializer<T extends Resource> extends StdDeserializer<T> {

    private final Class<T> clazz;

    public SpringDeserializer(Class<T> clazz) {
        super(clazz);
        this.clazz = clazz;
    }

    @Override
    public T deserialize(JsonParser jsonParser, DeserializationContext deserializationContext) throws IOException {
        String json = jsonParser.readValueAsTree().toString();
        return FhirHelper.deserialize(json, clazz);
    }
}
