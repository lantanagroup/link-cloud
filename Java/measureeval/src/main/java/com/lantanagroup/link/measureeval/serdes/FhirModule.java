package com.lantanagroup.link.measureeval.serdes;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.module.SimpleModule;
import com.lantanagroup.link.measureeval.utils.FhirContextUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;

public class FhirModule extends SimpleModule {
    public FhirModule(FhirContext fhirContext) {
        register(IBaseResource.class, fhirContext);
        for (Class<? extends IBaseResource> resourceType : FhirContextUtils.getResourceTypes(fhirContext)) {
            register(resourceType, fhirContext);
        }
    }

    private <T extends IBaseResource> void register(Class<T> resourceType, FhirContext fhirContext) {
        addDeserializer(resourceType, new FhirResourceDeserializer<>(resourceType, fhirContext));
        addSerializer(resourceType, new FhirResourceSerializer<>(fhirContext));
    }
}
