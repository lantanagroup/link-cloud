package com.lantanagroup.link.validation.services.execution.executors;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.IValidationSupport;
import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.r4.model.Coding;
import org.hl7.fhir.r4.model.Observation;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class ValueSetCheckExecutorTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();
    private static final IFhirPath FHIR_PATH = FHIR_CONTEXT.newFhirPath();

    private final ValidationSupportChain chain = mock(ValidationSupportChain.class);
    private final ValueSetCheckExecutor executor =
            new ValueSetCheckExecutor(FHIR_PATH, chain, new ObjectMapper());

    private static ExecutionContext contextWithCoding(String system, String code) {
        Observation obs = new Observation();
        obs.getCode().addCoding(new Coding(system, code, "display"));
        return ExecutionContext.builder().resource(obs).build();
    }

    private static RubricCheck check(String parametersJson) {
        return RubricCheck.builder()
                .checkLocalId("vs-1")
                .dimension(PiqiDimension.TERMINOLOGY)
                .parametersJson(parametersJson)
                .build();
    }

    @Test
    @DisplayName("supports VALUESET")
    void supportsValueSet() {
        assertThat(executor.supports()).isEqualTo(CheckType.VALUESET);
    }

    @Test
    @DisplayName("a code outside the value set produces a membership-failed finding")
    void codeOutsideValueSetFails() {
        IValidationSupport.CodeValidationResult result = mock(IValidationSupport.CodeValidationResult.class);
        when(result.isOk()).thenReturn(false);
        when(chain.validateCode(any(), any(), eq("http://loinc.org"), eq("9999-9"), any(), eq("http://x/vs")))
                .thenReturn(result);

        List<RawFinding> findings = executor.execute(
                check("{\"path\":\"Observation.code\",\"valueSet\":\"http://x/vs\"}"),
                contextWithCoding("http://loinc.org", "9999-9"));

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("valueset-membership-failed");
        assertThat(findings.get(0).getLocation()).isEqualTo("Observation.code");
    }

    @Test
    @DisplayName("a code in the value set produces no findings")
    void codeInValueSetPasses() {
        IValidationSupport.CodeValidationResult result = mock(IValidationSupport.CodeValidationResult.class);
        when(result.isOk()).thenReturn(true);
        when(chain.validateCode(any(), any(), any(), any(), any(), any())).thenReturn(result);

        assertThat(executor.execute(
                check("{\"path\":\"Observation.code\",\"valueSet\":\"http://x/vs\"}"),
                contextWithCoding("http://loinc.org", "1234-5"))).isEmpty();
    }

    @Test
    @DisplayName("missing path or valueSet -> no findings")
    void missingParametersProducesNoFindings() {
        assertThat(executor.execute(check("{\"valueSet\":\"http://x/vs\"}"),
                contextWithCoding("http://loinc.org", "1234-5"))).isEmpty();
        assertThat(executor.execute(check("{\"path\":\"Observation.code\"}"),
                contextWithCoding("http://loinc.org", "1234-5"))).isEmpty();
    }
}
