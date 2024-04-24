package com.lantanagroup.link.shared.fhir;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import org.hl7.fhir.r4.model.Resource;

import java.io.InputStream;

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

    public static Resource deserialize(InputStream is) {
        return (Resource) getParser().parseResource(is);
    }

    public static <T extends Resource> T deserialize(String json, Class<T> clazz) {
        return getParser().parseResource(clazz, json);
    }

    public static String serialize(Resource resource) {
        return getParser().encodeResourceToString(resource);
    }
}
