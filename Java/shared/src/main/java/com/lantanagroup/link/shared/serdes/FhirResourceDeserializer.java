package com.lantanagroup.link.shared.serdes;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.DataFormatException;
import ca.uhn.fhir.parser.IParser;
import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.deser.std.StdDeserializer;
import com.lantanagroup.link.shared.exceptions.FhirParseException;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.io.IOException;

public class FhirResourceDeserializer<T extends IBaseResource> extends StdDeserializer<T> {
    private final Class<T> resourceType;
    private final FhirContext fhirContext;

    public FhirResourceDeserializer(Class<T> resourceType, FhirContext fhirContext) {
        super(resourceType);
        this.resourceType = resourceType;
        this.fhirContext = fhirContext;
    }

    @Override
    public T deserialize(JsonParser jsonParser, DeserializationContext deserializationContext) throws IOException {
        IParser parser = fhirContext.newJsonParser();
        String json = jsonParser.readValueAsTree().toString();
        try {
            if (resourceType == IBaseResource.class) {
                return resourceType.cast(parser.parseResource(json));
            }
            return parser.parseResource(resourceType, json);
        } catch (DataFormatException e) {
            throw new FhirParseException(e);
        }
    }
}
