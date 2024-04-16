package com.lantanagroup.link.shared.fhir;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import org.hl7.fhir.r4.model.Resource;

public class FhirHelper {
    private static FhirContext context;
    private static IParser parser;

    public static FhirContext getContext() {
        if (context == null) {
            context = FhirContext.forR4();
        }
        return context;
    }

    public static IParser getParser() {
        if (parser == null) {
            parser = getContext().newJsonParser();
        }
        return parser;
    }

    public static Resource deserialize(String json) {
        return (Resource) getParser().parseResource(json);
    }

    public static <T extends Resource> T deserialize(String json, Class<T> clazz) {
        return getParser().parseResource(clazz, json);
    }

    public static String serialize(Resource resource) {
        return getParser().encodeResourceToString(resource);
    }
}
