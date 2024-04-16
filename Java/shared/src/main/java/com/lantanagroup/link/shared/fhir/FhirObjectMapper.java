package com.lantanagroup.link.shared.fhir;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.module.SimpleModule;
import org.hl7.fhir.r4.model.Bundle;

public class FhirObjectMapper extends ObjectMapper {
    public FhirObjectMapper() {
        SimpleModule module = new SimpleModule();

        module.addSerializer(new SpringSerializer<>(Bundle.class));
        module.addDeserializer(Bundle.class, new SpringDeserializer<>(Bundle.class));

        this.registerModule(module);
    }
}
