package com.lantanagroup.link.shared.utils;

import ca.uhn.fhir.context.BaseRuntimeElementDefinition;
import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.RuntimePrimitiveDatatypeDefinition;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.instance.model.api.IPrimitiveType;

import java.util.List;
import java.util.stream.Collectors;

public class FhirContextUtils {
    public static List<Class<? extends IPrimitiveType<?>>> getPrimitiveTypes(FhirContext fhirContext) {
        return fhirContext.getElementDefinitions().stream()
                .filter(RuntimePrimitiveDatatypeDefinition.class::isInstance)
                .map(RuntimePrimitiveDatatypeDefinition.class::cast)
                .map(BaseRuntimeElementDefinition::getImplementingClass)
                .collect(Collectors.toList());
    }

    public static List<Class<? extends IBaseResource>> getResourceTypes(FhirContext fhirContext) {
        return fhirContext.getResourceTypes().stream()
                .map(fhirContext::getResourceDefinition)
                .map(BaseRuntimeElementDefinition::getImplementingClass)
                .collect(Collectors.toList());
    }
}
