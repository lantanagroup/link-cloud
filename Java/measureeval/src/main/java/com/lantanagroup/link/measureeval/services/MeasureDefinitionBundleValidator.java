package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.Measure;
import org.hl7.fhir.r4.model.PrimitiveType;
import org.springframework.stereotype.Service;

import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;

@Service
public class MeasureDefinitionBundleValidator extends Validator<Bundle> {
    @Override
    protected void doValidate(Bundle bundle, List<String> errors) {
        List<Measure> measures = bundle.getEntry().stream()
                .map(Bundle.BundleEntryComponent::getResource)
                .filter(Measure.class::isInstance)
                .map(Measure.class::cast)
                .toList();
        if (measures.isEmpty()) {
            errors.add("Bundle must contain a measure");
        } else if (measures.size() > 1) {
            errors.add("Bundle must contain a single measure");
        } else {
            Measure measure = measures.get(0);
            doValidate(bundle, measure, errors);
        }
    }

    private void doValidate(Bundle bundle, Measure measure, List<String> errors) {
        if (!measure.hasUrl()) {
            errors.add("Measure must have a URL");
        }
        Set<String> libraryUrls = measure.getLibrary().stream()
                .map(PrimitiveType::asStringValue)
                .collect(Collectors.toSet());
        Set<String> existingLibraryUrls = bundle.getEntry().stream()
                .map(Bundle.BundleEntryComponent::getResource)
                .filter(Library.class::isInstance)
                .map(Library.class::cast)
                .map(Library::getUrl)
                .collect(Collectors.toSet());
        if (!existingLibraryUrls.containsAll(libraryUrls)) {
            errors.add("Libraries referenced by the measure must be contained in the bundle");
        }
    }
}
