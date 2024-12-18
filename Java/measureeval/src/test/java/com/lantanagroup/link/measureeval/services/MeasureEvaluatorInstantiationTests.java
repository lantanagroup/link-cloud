package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.server.exceptions.ResourceNotFoundException;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.Measure;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;

/**
 * Test class for validating the instantiation of the {@link MeasureEvaluator}.
 * This class ensures proper error handling and validation of input artifacts
 * such as {@link Bundle}, {@link Measure}, and {@link Library}.
 */
class MeasureEvaluatorInstantiationTests {

    private final FhirContext fhirContext = FhirContext.forR4Cached();

    /**
     * Tests that an {@link IllegalArgumentException} is thrown when attempting to compile a {@link MeasureEvaluator}
     * using an unsupported FHIR R5 context.
     */
    @Test
    void newInstanceWithR5FhirContextTest() {
        FhirContext r5FhirContext = FhirContext.forR5Cached();
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(new Measure());
        Assertions.assertThrows(IllegalArgumentException.class,
                () -> MeasureEvaluator.compile(r5FhirContext, bundle),
                "Unsupported FHIR version!");
    }

    /**
     * Tests that an {@link IllegalArgumentException} is thrown when the provided {@link Bundle} is empty.
     */
    @Test
    void newInstanceEmptyBundleTest() {
        Bundle emptyBundle = new Bundle();
        Assertions.assertThrows(IllegalArgumentException.class,
                () -> MeasureEvaluator.compile(fhirContext, emptyBundle),
                "Please provide the necessary artifacts (e.g. Measure and Library resources) in the Bundle entry!");
    }

    /**
     * Tests that an {@link IllegalArgumentException} is thrown when a {@link Measure} does not include
     * a primary library reference.
     */
    @Test
    void newInstanceMeasureWithoutPrimaryLibraryReference() {
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(createMeasure(false));
        Assertions.assertThrows(IllegalArgumentException.class, () -> MeasureEvaluator.compile(fhirContext, bundle),
                "Measure null does not have a primary library specified");
    }

    /**
     * Tests that a {@link ResourceNotFoundException} is thrown when the {@link Measure} references a non-existent
     * primary {@link Library}.
     */
    @Test
    void newInstanceMeasureWithMissingPrimaryLibraryReference() {
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(createMeasure(true));
        Assertions.assertThrows(ResourceNotFoundException.class, () -> MeasureEvaluator.compile(fhirContext, bundle),
                "Unable to find Library with url: https://example.com/Library/Nonexistent");
    }

    /**
     * Tests that an {@link IllegalStateException} is thrown when the primary {@link Library} does not contain
     * embedded CQL content.
     */
    @Test
    void newInstanceMeasureWithPrimaryLibraryReferenceWithoutCqlContent() {
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(createMeasure(true));
        bundle.addEntry().setResource(createLibrary(false));
        Assertions.assertThrows(IllegalStateException.class, () -> MeasureEvaluator.compile(fhirContext, bundle),
                "Unable to load CQL/ELM for library: Nonexistent. Verify that the Library resource is available in your environment and has CQL/ELM content embedded.");
    }

    /**
     * Tests that no exceptions are thrown when a valid {@link Measure} and {@link Library} with CQL content are provided.
     */
    @Test
    void newInstanceMeasureWithPrimaryLibraryReferenceWithCqlContent() {
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(createMeasure(true));
        bundle.addEntry().setResource(createLibrary(true));
        Assertions.assertDoesNotThrow(() -> MeasureEvaluator.compile(fhirContext, bundle));
    }

    /**
     * Helper method to create a {@link Measure} with or without a primary library reference.
     *
     * @param hasPrimaryLibraryReference Whether the measure should reference a primary library.
     * @return A {@link Measure} instance.
     */
    private Measure createMeasure(boolean hasPrimaryLibraryReference) {
        Measure measure = new Measure().addLibrary(
                hasPrimaryLibraryReference ? "https://example.com/Library/Nonexistent" : null);
        measure.setId("test");
        return measure;
    }

    /**
     * Helper method to create a {@link Library} with or without embedded CQL content.
     *
     * @param hasContent Whether the library should contain CQL content.
     * @return A {@link Library} instance.
     */
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
