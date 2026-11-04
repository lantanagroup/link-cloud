package com.lantanagroup.link.validation.services.execution.spi;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import org.hl7.fhir.r4.model.Observation;
import org.hl7.fhir.r4.model.Quantity;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class NumericRangeCustomCheckTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();
    private static final IFhirPath FHIR_PATH = FHIR_CONTEXT.newFhirPath();

    private final NumericRangeCustomCheck check = new NumericRangeCustomCheck(FHIR_PATH, new ObjectMapper());

    private static RubricCheck rubricCheck(String parametersJson) {
        return RubricCheck.builder()
                .checkLocalId("numeric-range-1")
                .dimension(PiqiDimension.PLAUSIBILITY)
                .parametersJson(parametersJson)
                .build();
    }

    private static ExecutionContext contextWith(double value) {
        Observation observation = new Observation();
        observation.setValue(new Quantity().setValue(value));
        return ExecutionContext.builder().resource(observation).build();
    }

    @Test
    @DisplayName("value above max -> flagged")
    void aboveMaxIsFlagged() {
        List<RawFinding> findings = check.run(
                rubricCheck("{\"path\":\"Observation.value\",\"min\":0,\"max\":100}"), contextWith(250));

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("numeric-out-of-range");
    }

    @Test
    @DisplayName("value within range -> no findings")
    void withinRangeNotFlagged() {
        List<RawFinding> findings = check.run(
                rubricCheck("{\"path\":\"Observation.value\",\"min\":0,\"max\":100}"), contextWith(50));

        assertThat(findings).isEmpty();
    }

    @Test
    @DisplayName("non-numeric bounds are unusable -> check returns no findings instead of comparing against 0")
    void nonNumericBoundsAreRejected() {
        // with the old asDouble() parsing, min "abc" became 0 and -5 was flagged
        List<RawFinding> findings = check.run(
                rubricCheck("{\"path\":\"Observation.value\",\"min\":\"abc\"}"), contextWith(-5));

        assertThat(findings).isEmpty();
    }

    @Test
    @DisplayName("context with neither resource nor bundle entries -> no findings, no NPE")
    void noTargetsReturnsEmpty() {
        List<RawFinding> findings = check.run(
                rubricCheck("{\"path\":\"Observation.value\",\"min\":0}"), new ExecutionContext());

        assertThat(findings).isEmpty();
    }
}
