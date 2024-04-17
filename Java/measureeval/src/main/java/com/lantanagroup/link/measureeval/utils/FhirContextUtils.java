package com.lantanagroup.link.measureeval.utils;

import ca.uhn.fhir.context.BaseRuntimeElementDefinition;
import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.util.List;
import java.util.stream.Collectors;

public class FhirContextUtils {
    public static List<Class<? extends IBaseResource>> getResourceTypes(FhirContext fhirContext) {
        return fhirContext.getResourceTypes().stream()
                .map(fhirContext::getResourceDefinition)
                .map(BaseRuntimeElementDefinition::getImplementingClass)
                .collect(Collectors.toList());
    }
}
