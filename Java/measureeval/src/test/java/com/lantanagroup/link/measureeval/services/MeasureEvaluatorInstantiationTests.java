package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.server.exceptions.ResourceNotFoundException;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.Measure;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;

class MeasureEvaluatorInstantiationTests {

    private final FhirContext fhirContext = FhirContext.forR4Cached();

    @Test
    void newInstanceWithR5FhirContextTest() {
        FhirContext r5FhirContext = FhirContext.forR5Cached();
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(new Measure());
        Assertions.assertThrows(IllegalArgumentException.class,
                () -> MeasureEvaluator.compile(r5FhirContext, bundle),
                "Unsupported FHIR version!");
    }

    @Test
    void newInstanceEmptyBundleTest() {
        Bundle emptyBundle = new Bundle();
        Assertions.assertThrows(IllegalArgumentException.class,
                () -> MeasureEvaluator.compile(fhirContext, emptyBundle),
                "Please provide the necessary artifacts (e.g. Measure and Library resources) in the Bundle entry!");
    }

    @Test
    void newInstanceMeasureWithoutPrimaryLibraryReference() {
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(createMeasure(false));
        Assertions.assertThrows(IllegalArgumentException.class, () -> MeasureEvaluator.compile(fhirContext, bundle),
                "Measure null does not have a primary library specified");
    }

    @Test
    void newInstanceMeasureWithMissingPrimaryLibraryReference() {
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(createMeasure(true));
        Assertions.assertThrows(ResourceNotFoundException.class, () -> MeasureEvaluator.compile(fhirContext, bundle),
                "Unable to find Library with url: https://example.com/Library/Nonexistent");
    }

    @Test
    void newInstanceMeasureWithPrimaryLibraryReferenceWithoutCqlContent() {
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(createMeasure(true));
        bundle.addEntry().setResource(createLibrary(false));
        Assertions.assertThrows(IllegalStateException.class, () -> MeasureEvaluator.compile(fhirContext, bundle),
                "Unable to load CQL/ELM for library: Nonexistent. Verify that the Library resource is available in your environment and has CQL/ELM content embedded.");
    }

    @Test
    void newInstanceMeasureWithPrimaryLibraryReferenceWithCqlContent() {
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(createMeasure(true));
        bundle.addEntry().setResource(createLibrary(true));
        Assertions.assertDoesNotThrow(() -> MeasureEvaluator.compile(fhirContext, bundle));
    }

    // Builders
    private Measure createMeasure(boolean hasPrimaryLibraryReference) {
        Measure measure = new Measure().addLibrary(
                hasPrimaryLibraryReference ? "https://example.com/Library/Nonexistent" : null);
        measure.setId("test");
        return measure;
    }

    private Library createLibrary(boolean hasContent) {
        Library library = new Library().setUrl("https://example.com/Library/Nonexistent").setVersion("1.0.0");
        library.setId("Nonexistent");
        library.setName("Nonexistent");
        if (hasContent) {
            library.addContent().setContentType("text/cql").setData(
                    "library Nonexistent version '1.0.0'".getBytes(StandardCharsets.UTF_8));
        }
        return library;
    }
}
