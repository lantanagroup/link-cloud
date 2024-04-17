package com.lantanagroup.link.measureeval.serdes;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.utils.FhirContextUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;

public class FhirMongoConverterConfigurationAdapter extends MongoCustomConversions.MongoConverterConfigurationAdapter {
    public FhirMongoConverterConfigurationAdapter(FhirContext fhirContext) {
        registerConverters(fhirContext, IBaseResource.class);
        for (Class<? extends IBaseResource> resourceType : FhirContextUtils.getResourceTypes(fhirContext)) {
            registerConverters(fhirContext, resourceType);
        }
    }

    private <T extends IBaseResource> void registerConverters(FhirContext fhirContext, Class<T> resourceType) {
        registerConverter(new FhirResourceDeserializer<>(resourceType, fhirContext));
        registerConverter(new FhirResourceSerializer<>(resourceType, fhirContext));
    }
}
