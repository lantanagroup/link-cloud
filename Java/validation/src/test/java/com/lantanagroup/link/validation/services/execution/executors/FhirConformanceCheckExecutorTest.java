package com.lantanagroup.link.validation.services.execution.executors;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.ResultSeverityEnum;
import ca.uhn.fhir.validation.SingleValidationMessage;
import ca.uhn.fhir.validation.ValidationOptions;
import ca.uhn.fhir.validation.ValidationResult;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Patient;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class FhirConformanceCheckExecutorTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();

    private final FhirValidator fhirValidator = mock(FhirValidator.class);
    private final FhirConformanceCheckExecutor executor =
            new FhirConformanceCheckExecutor(fhirValidator, new ObjectMapper());

    private static RubricCheck check() {
        return RubricCheck.builder().checkLocalId("fc-1").dimension(PiqiDimension.CONFORMANCE).build();
    }

    private static ExecutionContext context() {
        return ExecutionContext.builder().resource(new Patient()).build();
    }

    private static SingleValidationMessage message(ResultSeverityEnum severity, String text) {
        SingleValidationMessage msg = new SingleValidationMessage();
        msg.setSeverity(severity);
        msg.setMessage(text);
        msg.setLocationString("Patient");
        return msg;
    }

    private void stubValidator(SingleValidationMessage... messages) {
        ValidationResult result = new ValidationResult(FHIR_CONTEXT, List.of(messages));
        when(fhirValidator.validateWithResult(any(IBaseResource.class), any(ValidationOptions.class)))
                .thenReturn(result);
    }

    @Test
    @DisplayName("supports FHIR_CONFORMANCE")
    void supportsConformance() {
        assertThat(executor.supports()).isEqualTo(CheckType.FHIR_CONFORMANCE);
    }

    @Test
    @DisplayName("validation messages become findings with mapped severities")
    void messagesBecomeFindings() {
        stubValidator(
                message(ResultSeverityEnum.ERROR, "bad"),
                message(ResultSeverityEnum.WARNING, "meh"),
                message(ResultSeverityEnum.INFORMATION, "fyi"));

        List<RawFinding> findings = executor.execute(check(), context());

        assertThat(findings).hasSize(3);
        assertThat(findings).extracting(RawFinding::getSeverity)
                .containsExactly(Severity.ERROR, Severity.WARNING, Severity.INFORMATION);
        assertThat(findings).allSatisfy(f -> assertThat(f.getCode()).isEqualTo("fhir-conformance"));
    }

    @Test
    @DisplayName("FATAL validation severity maps to ERROR")
    void fatalMapsToError() {
        stubValidator(message(ResultSeverityEnum.FATAL, "fatal"));

        List<RawFinding> findings = executor.execute(check(), context());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.ERROR);
    }

    @Test
    @DisplayName("a clean validation result produces no findings")
    void cleanResultProducesNoFindings() {
        stubValidator();

        assertThat(executor.execute(check(), context())).isEmpty();
    }
}
