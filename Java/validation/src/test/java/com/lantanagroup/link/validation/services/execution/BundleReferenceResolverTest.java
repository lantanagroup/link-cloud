package com.lantanagroup.link.validation.services.execution;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.BooleanType;
import org.hl7.fhir.r4.model.Patient;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.ServiceRequest;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Map;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;

class BundleReferenceResolverTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();

    private static final String TARGET_PATIENT =
            "ServiceRequest.subject.all(resolve().is(Patient))";

    private final BundleReferenceResolver resolver = new BundleReferenceResolver();

    @Test
    @DisplayName("buildIndex keys each resource by 'ResourceType/id' and by bare id")
    void buildIndexKeys() {
        Patient p = new Patient();
        p.setId("p1");
        Map<String, IBaseResource> index = BundleReferenceResolver.buildIndex(List.of(p));
        assertThat(index).containsKeys("Patient/p1", "p1");
        assertThat(index.get("Patient/p1")).isSameAs(p);
    }

    @Test
    @DisplayName("resolve() follows a reference to a sibling resource once the bundle is bound — the reference-target check passes")
    void resolveFollowsReferenceWhenBound() {
        Patient patient = new Patient();
        patient.setId("p1");
        ServiceRequest sr = new ServiceRequest();
        sr.setId("sr1");
        sr.setSubject(new Reference("Patient/p1"));

        resolver.bind(BundleReferenceResolver.buildIndex(List.of(sr, patient)));
        try {
            IFhirPath fhirPath = FHIR_CONTEXT.newFhirPath();
            fhirPath.setEvaluationContext(resolver);
            Optional<BooleanType> result = fhirPath.evaluateFirst(sr, TARGET_PATIENT, BooleanType.class);
            assertThat(result).isPresent();
            assertThat(result.get().booleanValue()).isTrue();
        } finally {
            resolver.clear();
        }
    }

    @Test
    @DisplayName("without a bound bundle resolve() is empty and the target check fails — the pre-fix behaviour this fix removes")
    void resolveEmptyWhenNotBound() {
        ServiceRequest sr = new ServiceRequest();
        sr.setId("sr1");
        sr.setSubject(new Reference("Patient/p1")); // a perfectly valid reference

        resolver.clear();
        IFhirPath fhirPath = FHIR_CONTEXT.newFhirPath();
        fhirPath.setEvaluationContext(resolver);
        Optional<BooleanType> result = fhirPath.evaluateFirst(sr, TARGET_PATIENT, BooleanType.class);
        // resolve() returns nothing, so all(resolve().is(Patient)) is not true
        assertThat(result.map(BooleanType::booleanValue).orElse(false)).isFalse();
    }

    @Test
    @DisplayName("a reference whose target is not in the bundle still does not resolve — a genuinely dangling reference stays a failure")
    void danglingReferenceStaysUnresolved() {
        Patient other = new Patient();
        other.setId("someone-else");
        ServiceRequest sr = new ServiceRequest();
        sr.setId("sr1");
        sr.setSubject(new Reference("Patient/missing"));

        resolver.bind(BundleReferenceResolver.buildIndex(List.of(sr, other)));
        try {
            IFhirPath fhirPath = FHIR_CONTEXT.newFhirPath();
            fhirPath.setEvaluationContext(resolver);
            Optional<BooleanType> result = fhirPath.evaluateFirst(sr, TARGET_PATIENT, BooleanType.class);
            assertThat(result.map(BooleanType::booleanValue).orElse(false)).isFalse();
        } finally {
            resolver.clear();
        }
    }
}
