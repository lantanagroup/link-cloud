package com.lantanagroup.link.measureeval.serdes;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.JsonDeserializer;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.io.IOException;

public class FhirResourceDeserializer<T extends IBaseResource> extends JsonDeserializer<T> {
    private final Class<T> resourceType;
    private final FhirContext fhirContext;

    public FhirResourceDeserializer(Class<T> resourceType, FhirContext fhirContext) {
        this.resourceType = resourceType;
        this.fhirContext = fhirContext;
    }

    @Override
    public T deserialize(JsonParser jsonParser, DeserializationContext deserializationContext) throws IOException {
        IParser parser = fhirContext.newJsonParser();
        String json = jsonParser.readValueAsTree().toString();
        if (resourceType == IBaseResource.class) {
            return resourceType.cast(parser.parseResource(json));
        }
        return parser.parseResource(resourceType, json);
    }
}
