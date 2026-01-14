package com.lantanagroup.link.shared.serdes;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.shared.utils.FhirContextUtils;
import jakarta.annotation.Nonnull;
import org.bson.Document;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.core.convert.TypeDescriptor;
import org.springframework.core.convert.converter.GenericConverter;

import java.util.Collections;
import java.util.HashSet;
import java.util.Set;
import java.util.stream.Collectors;

public class FhirResourceWriter implements GenericConverter {
    private final FhirContext fhirContext;
    private final Set<ConvertiblePair> convertiblePairs;

    public FhirResourceWriter(FhirContext fhirContext) {
        this.fhirContext = fhirContext;
        convertiblePairs = FhirContextUtils.getResourceTypes(fhirContext).stream()
                .map(resourceType -> new ConvertiblePair(resourceType, Document.class))
                .collect(Collectors.toCollection(HashSet::new));
        convertiblePairs.add(new ConvertiblePair(IBaseResource.class, Document.class));
    }

    @Override
    public Set<ConvertiblePair> getConvertibleTypes() {
        return Collections.unmodifiableSet(convertiblePairs);
    }

    @Override
    public Object convert(Object source, @Nonnull TypeDescriptor sourceType, @Nonnull TypeDescriptor targetType) {
        IBaseResource resource = (IBaseResource) source;
        String json = fhirContext.newJsonParser().encodeResourceToString(resource);
        return Document.parse(json);
    }
}
