package com.lantanagroup.link.shared.serdes;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.deser.std.StdDeserializer;
import org.hl7.fhir.r4.model.IdType;

import java.io.IOException;

public class FhirIdDeserializer extends StdDeserializer<String> {
    public FhirIdDeserializer() {
        super(String.class);
    }

    @Override
    public String deserialize(JsonParser jsonParser, DeserializationContext deserializationContext) throws IOException {
        String value = jsonParser.getText();
        IdType id = new IdType(value);
        return id.getIdPart();
    }
}
