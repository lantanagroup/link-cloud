package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.models.FhirPathValidationResponse;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class FhirPathValidationServiceTest {

    private static FhirPathValidationService service;

    @BeforeAll
    static void setUp() {
        // R4 worker context construction is expensive; build the service once for all cases.
        service = new FhirPathValidationService(FhirContext.forR4());
    }

    @Test
    void validExpression_passes() {
        FhirPathValidationResponse result =
                service.validate("Location", "Location.type.coding.exists(code = '783')");

        assertTrue(result.isValid(), () -> "expected valid, errors: " + result.getErrors());
        assertTrue(result.getErrors().isEmpty());
    }

    @Test
    void validBooleanComparison_passes() {
        FhirPathValidationResponse result =
                service.validate("Location", "Location.status = 'active'");

        assertTrue(result.isValid(), () -> "expected valid, errors: " + result.getErrors());
    }

    @Test
    void compoundBooleanExpression_passes() {
        // Parenthesized 'or' of two exists() checks over a real element; resolves to boolean.
        FhirPathValidationResponse result = service.validate(
                "Location",
                "(Location.identifier.exists(system = 'test' and value = 'test')) "
                        + "or (Location.identifier.exists(system = 'test' and value = 'test'))");

        assertTrue(result.isValid(), () -> "expected valid, errors: " + result.getErrors());
        assertTrue(result.getErrors().isEmpty());
    }

    @Test
    void compoundBooleanExpression_encounter_passes() {
        // Same compound 'or'/exists() shape, validated against the Encounter type.
        FhirPathValidationResponse result = service.validate(
                "Encounter",
                "(Encounter.identifier.exists(system = 'test' and value = 'test')) "
                        + "or (Encounter.identifier.exists(system = 'test' and value = 'test'))");

        assertTrue(result.isValid(), () -> "expected valid, errors: " + result.getErrors());
        assertTrue(result.getErrors().isEmpty());
    }

    @Test
    void unknownElement_fails() {
        // 'managingOrg' is not a real Location element (it's 'managingOrganization').
        FhirPathValidationResponse result =
                service.validate("Location", "Location.managingOrg.exists()");

        assertFalse(result.isValid());
        assertFalse(result.getErrors().isEmpty());
    }

    @Test
    void syntaxError_fails() {
        FhirPathValidationResponse result =
                service.validate("Location", "Location.type.coding.exists(code = ");

        assertFalse(result.isValid());
        assertFalse(result.getErrors().isEmpty());
    }

    @Test
    void nonBooleanReturn_warns() {
        // 'Location.name' resolves but returns a string, not a boolean condition.
        FhirPathValidationResponse result =
                service.validate("Location", "Location.name");

        assertTrue(result.isValid(), () -> "expected valid, errors: " + result.getErrors());
        assertFalse(result.getWarnings().isEmpty(), "expected a non-boolean return-type warning");
    }

    @Test
    void missingInputs_fail() {
        assertFalse(service.validate(null, "Location.status.exists()").isValid());
        assertFalse(service.validate("Location", "  ").isValid());
    }
}
