package com.lantanagroup.link.measureeval.serdes;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.module.SimpleModule;
import com.lantanagroup.link.measureeval.utils.FhirContextUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;

public class FhirAwareModule extends SimpleModule {
    public FhirAwareModule(FhirContext fhirContext) {
        addSerializers(fhirContext, IBaseResource.class);
        for (Class<? extends IBaseResource> resourceType : FhirContextUtils.getResourceTypes(fhirContext)) {
            addSerializers(fhirContext, resourceType);
        }
    }

    private <T extends IBaseResource> void addSerializers(FhirContext fhirContext, Class<T> resourceType) {
        addDeserializer(resourceType, new FhirResourceDeserializer<>(resourceType, fhirContext));
        addSerializer(resourceType, new FhirResourceSerializer<>(resourceType, fhirContext));
    }
}
