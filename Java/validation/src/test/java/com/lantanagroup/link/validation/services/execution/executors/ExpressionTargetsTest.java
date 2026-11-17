package com.lantanagroup.link.validation.services.execution.executors;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;

class ExpressionTargetsTest {

    @Test
    void aPathStartingWithOneTypeSelectsOnlyFromThatType() {
        assertEquals("Observation", ExpressionTargets.onlyResourceType("Observation.code"));
        assertEquals("Encounter", ExpressionTargets.onlyResourceType("Encounter.hospitalization.admitSource"));
        assertEquals("Patient", ExpressionTargets.onlyResourceType(
                "Patient.extension.where(url = 'http://hl7.org/fhir/us/core/StructureDefinition/us-core-birthsex').value"));
        // A | inside a string literal doesn't combine expressions.
        assertEquals("Observation", ExpressionTargets.onlyResourceType("Observation.code.where(text = 'a|b')"));
    }

    @Test
    void aPathThatMaySelectFromSeveralTypesHasNone() {
        assertNull(ExpressionTargets.onlyResourceType("Observation.code | Condition.code"));
        assertNull(ExpressionTargets.onlyResourceType("Observation.code.union(Condition.code)"));
        assertNull(ExpressionTargets.onlyResourceType("code"));
        assertNull(ExpressionTargets.onlyResourceType("%resource.code"));
        assertNull(ExpressionTargets.onlyResourceType("Observation.code.combine(Condition.code)"));
        assertNull(ExpressionTargets.onlyResourceType("Observation.iif(status = 'final', code, Condition.code)"));
        assertNull(ExpressionTargets.onlyResourceType("Observation.code.union (Condition.code)"));
    }

    @Test
    void anAbstractTypeMatchesEveryResourceDerivedFromItSoNarrowsNothing() {
        // FHIRPath matches a leading type against the resource's type and its ancestors (checked against HAPI).
        assertNull(ExpressionTargets.leadingResourceType("Resource.meta.security"));
        assertNull(ExpressionTargets.leadingResourceType("DomainResource.text.status"));
        assertNull(ExpressionTargets.leadingResourceType("MetadataResource.status"));
        assertNull(ExpressionTargets.onlyResourceType("Resource.meta.security"));
        assertNull(ExpressionTargets.onlyResourceType("(DomainResource.text.status)"));
    }

    @Test
    void wordsThatOnlyResembleTheCombiningFunctionsDoNotCount() {
        assertEquals("Observation", ExpressionTargets.onlyResourceType("Observation.extension.where(url = 'x').valueString"));
        assertEquals("Observation", ExpressionTargets.onlyResourceType("Observation.code.where(text = 'union(a)')"));
    }
}
