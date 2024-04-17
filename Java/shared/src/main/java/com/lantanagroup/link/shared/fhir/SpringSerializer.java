package com.lantanagroup.link.shared.fhir;

import com.fasterxml.jackson.core.JsonGenerator;
import com.fasterxml.jackson.databind.SerializerProvider;
import com.fasterxml.jackson.databind.ser.std.StdSerializer;
import org.hl7.fhir.r4.model.Resource;

import java.io.IOException;

public class SpringSerializer<T extends Resource> extends StdSerializer<T> {
    public SpringSerializer(Class<T> t) {
        super(t);
    }

    @Override
    public void serialize(T bundle, JsonGenerator jsonGenerator, SerializerProvider serializerProvider) throws IOException {
        String json = FhirHelper.serialize(bundle);
        jsonGenerator.writeRawValue(json);
    }
}
