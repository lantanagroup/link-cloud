package com.lantanagroup.link.validation.services.execution.executors;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.IValidationSupport;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
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
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class TerminologyCheckExecutorTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();

    private final ValidationSupportChain chain = mock(ValidationSupportChain.class);
    private final TerminologyCheckExecutor executor =
            new TerminologyCheckExecutor(FHIR_CONTEXT, chain, new ObjectMapper());

    private static ExecutionContext contextWithCoding(String system, String code) {
        Observation obs = new Observation();
        obs.getCode().addCoding(new Coding(system, code, "display"));
        return ExecutionContext.builder().resource(obs).build();
    }

    private static RubricCheck check(String parametersJson) {
        return RubricCheck.builder()
                .checkLocalId("term-1")
                .dimension(PiqiDimension.TERMINOLOGY)
                .parametersJson(parametersJson)
                .build();
    }

    private void stubValidateCode(boolean ok) {
        IValidationSupport.CodeValidationResult result = mock(IValidationSupport.CodeValidationResult.class);
        when(result.isOk()).thenReturn(ok);
        when(chain.validateCode(any(), any(), eq("http://loinc.org"), eq("1234-5"), any(), any()))
                .thenReturn(result);
    }

    private void stubValidateCode(boolean ok, String message) {
        IValidationSupport.CodeValidationResult result = mock(IValidationSupport.CodeValidationResult.class);
        when(result.isOk()).thenReturn(ok);
        when(result.getMessage()).thenReturn(message);
        when(chain.validateCode(any(), any(), eq("http://loinc.org"), eq("1234-5"), any(), any()))
                .thenReturn(result);
    }

    @Test
    @DisplayName("supports TERMINOLOGY")
    void supportsTerminology() {
        assertThat(executor.supports()).isEqualTo(CheckType.TERMINOLOGY);
    }

    @Test
    @DisplayName("an invalid code produces a WARNING finding by default")
    void invalidCodeProducesWarning() {
        stubValidateCode(false);

        List<RawFinding> findings = executor.execute(check(null), contextWithCoding("http://loinc.org", "1234-5"));

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("terminology-code-invalid");
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.WARNING);
        assertThat(findings.get(0).getExpression()).isEqualTo("http://loinc.org|1234-5");
    }

    @Test
    @DisplayName("an unresolvable code system yields a not-evaluated (INCONCLUSIVE) finding, not an invalid-code warning")
    void unresolvableCodeSystemIsNotEvaluated() {
        stubValidateCode(false, "Code system http://loinc.org could not be resolved");

        List<RawFinding> findings = executor.execute(check(null), contextWithCoding("http://loinc.org", "1234-5"));

        assertThat(findings).hasSize(1);
        RawFinding f = findings.get(0);
        assertThat(f.getCode()).isEqualTo("binding-not-evaluated");
        assertThat(f.getSeverity()).isEqualTo(Severity.INFORMATION);
        assertThat(f.isNotEvaluated()).isTrue();
    }

    @Test
    @DisplayName("a valid code produces no findings")
    void validCodeProducesNoFindings() {
        stubValidateCode(true);

        assertThat(executor.execute(check(null), contextWithCoding("http://loinc.org", "1234-5"))).isEmpty();
    }

    @Test
    @DisplayName("a whitelisted code system is skipped (never validated)")
    void whitelistedSystemIsSkipped() {
        List<RawFinding> findings = executor.execute(
                check("{\"valueSetWhitelistRegex\":\".*loinc.*\"}"),
                contextWithCoding("http://loinc.org", "1234-5"));

        assertThat(findings).isEmpty();
        verify(chain, never()).validateCode(any(), any(), any(), any(), any(), any());
    }

    @Test
    @DisplayName("validateCodings=false short-circuits with no findings")
    void validateCodingsFalseShortCircuits() {
        assertThat(executor.execute(check("{\"validateCodings\":false}"),
                contextWithCoding("http://loinc.org", "1234-5"))).isEmpty();
        verify(chain, never()).validateCode(any(), any(), any(), any(), any(), any());
    }
}
