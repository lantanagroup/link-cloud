package com.lantanagroup.link.shared.serdes;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.module.SimpleModule;
import com.lantanagroup.link.shared.utils.FhirContextUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;

public class FhirModule extends SimpleModule {
    public FhirModule(FhirContext fhirContext) {
        register(fhirContext, IBaseResource.class);
        for (Class<? extends IBaseResource> resourceType : FhirContextUtils.getResourceTypes(fhirContext)) {
            register(fhirContext, resourceType);
        }
    }

    private <T extends IBaseResource> void register(FhirContext fhirContext, Class<T> resourceType) {
        addDeserializer(resourceType, new FhirResourceDeserializer<>(resourceType, fhirContext));
        addSerializer(resourceType, new FhirResourceSerializer<>(resourceType, fhirContext));
    }
}
