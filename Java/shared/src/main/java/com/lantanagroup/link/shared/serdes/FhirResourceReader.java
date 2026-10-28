package com.lantanagroup.link.shared.serdes;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
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

public class FhirResourceReader implements GenericConverter {
    private final FhirContext fhirContext;
    private final Set<ConvertiblePair> convertiblePairs;

    public FhirResourceReader(FhirContext fhirContext) {
        this.fhirContext = fhirContext;
        convertiblePairs = FhirContextUtils.getResourceTypes(fhirContext).stream()
                .map(resourceType -> new ConvertiblePair(Document.class, resourceType))
                .collect(Collectors.toCollection(HashSet::new));
        convertiblePairs.add(new ConvertiblePair(Document.class, IBaseResource.class));
    }

    @Override
    public Set<ConvertiblePair> getConvertibleTypes() {
        return Collections.unmodifiableSet(convertiblePairs);
    }

    @Override
    public Object convert(Object source, @Nonnull TypeDescriptor sourceType, @Nonnull TypeDescriptor targetType) {
        Document document = (Document) source;
        String json = document.toJson();
        IParser parser = fhirContext.newJsonParser();
        Class<? extends IBaseResource> resourceType = targetType.getType().asSubclass(IBaseResource.class);
        return resourceType == IBaseResource.class
                ? parser.parseResource(json)
                : parser.parseResource(resourceType, json);
    }
}
