package com.lantanagroup.link.validation.services.execution.executors;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Patient;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class FhirPathCheckExecutorTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();
    private static final IFhirPath FHIR_PATH = FHIR_CONTEXT.newFhirPath();

    private final FhirPathCheckExecutor executor = new FhirPathCheckExecutor(FHIR_PATH, new ObjectMapper());

    private static RubricCheck check(String parametersJson, Severity severityOverride) {
        return RubricCheck.builder()
                .checkLocalId("fp-1")
                .dimension(PiqiDimension.CONFORMANCE)
                .severityOverride(severityOverride)
                .parametersJson(parametersJson)
                .build();
    }

    private static ExecutionContext context(Patient patient) {
        return ExecutionContext.builder().resource(patient).build();
    }

    @Test
    @DisplayName("supports FHIRPATH")
    void supportsFhirpath() {
        assertThat(executor.supports()).isEqualTo(CheckType.FHIRPATH);
    }

    @Test
    @DisplayName("a satisfied assertion produces no findings")
    void satisfiedAssertionPasses() {
        Patient patient = new Patient();
        patient.setActive(true);

        List<RawFinding> findings = executor.execute(
                check("{\"expression\":\"Patient.active\"}", null), context(patient));

        assertThat(findings).isEmpty();
    }

    @Test
    @DisplayName("a failed assertion produces one ERROR finding by default, carrying the expression")
    void failedAssertionProducesError() {
        Patient patient = new Patient();
        patient.setActive(false);

        List<RawFinding> findings = executor.execute(
                check("{\"expression\":\"Patient.active\"}", null), context(patient));

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("fhirpath-assertion-failed");
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.ERROR);
        assertThat(findings.get(0).getExpression()).isEqualTo("Patient.active");
    }

    @Test
    @DisplayName("a severity override on the check is applied to the finding")
    void severityOverrideApplied() {
        Patient patient = new Patient();
        patient.setActive(false);

        List<RawFinding> findings = executor.execute(
                check("{\"expression\":\"Patient.active\"}", Severity.WARNING), context(patient));

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.WARNING);
    }

    @Test
    @DisplayName("custom code and failureMessage from parameters are used")
    void customCodeAndMessage() {
        Patient patient = new Patient();
        patient.setActive(false);

        List<RawFinding> findings = executor.execute(
                check("{\"expression\":\"Patient.active\",\"code\":\"patient-inactive\",\"failureMessage\":\"must be active\"}", null),
                context(patient));

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("patient-inactive");
        assertThat(findings.get(0).getMessage()).isEqualTo("must be active");
    }

    @Test
    @DisplayName("missing parameters -> no findings")
    void missingParameters() {
        assertThat(executor.execute(check(null, null), context(new Patient()))).isEmpty();
    }

    @Test
    @DisplayName("missing expression -> no findings")
    void missingExpression() {
        assertThat(executor.execute(check("{}", null), context(new Patient()))).isEmpty();
    }

    // empty-Bundle payload: resource is the Bundle, bundleEntries is empty. Must NOT be treated
    // like a bare single resource (which would run resource-typed checks against the envelope).
    private static ExecutionContext emptyBundleContext() {
        return ExecutionContext.builder()
                .resource(new Bundle())
                .bundleEntries(List.of())
                .build();
    }

    @Test
    @DisplayName("empty bundle: a resource-typed check produces NO findings (not phantom findings against the Bundle)")
    void emptyBundleResourceTypedCheckHasNoFindings() {
        List<RawFinding> findings = executor.execute(
                check("{\"expression\":\"Patient.name.exists()\"}", null), emptyBundleContext());

        assertThat(findings).isEmpty();
    }

    @Test
    @DisplayName("empty bundle: a Bundle-level expression still evaluates against the Bundle so it can be flagged")
    void emptyBundleBundleLevelExpressionStillEvaluated() {
        List<RawFinding> findings = executor.execute(
                check("{\"expression\":\"Bundle.entry.count() >= 1\",\"code\":\"bundle-empty\"}", null),
                emptyBundleContext());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("bundle-empty");
        assertThat(findings.get(0).getLocation()).isEqualTo("Bundle");
    }

    @Test
    @DisplayName("non-empty bundle: a resource-typed check runs once per matching entry")
    void nonEmptyBundleTargetsMatchingEntries() {
        Patient withName = new Patient();
        withName.setId("with");
        withName.addName().setFamily("Doe");
        Patient withoutName = new Patient();
        withoutName.setId("without");
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(withName);
        bundle.addEntry().setResource(withoutName);
        ExecutionContext ctx = ExecutionContext.builder()
                .resource(bundle)
                .bundleEntries(List.of(withName, withoutName))
                .build();

        List<RawFinding> findings = executor.execute(
                check("{\"expression\":\"Patient.name.exists()\"}", null), ctx);

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getLocation()).isEqualTo("Patient/without");
    }
}
