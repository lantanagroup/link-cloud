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

    private static SingleValidationMessage message(ResultSeverityEnum severity, String text, String messageId) {
        SingleValidationMessage msg = message(severity, text);
        msg.setMessageId(messageId);
        return msg;
    }

    private static SingleValidationMessage messageAt(ResultSeverityEnum severity, String text, String messageId, String location) {
        SingleValidationMessage msg = message(severity, text, messageId);
        msg.setLocationString(location);
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

    // The terminology strings and message ids below are the VERBATIM output of HAPI 8.10's
    // InstanceValidator for unresolvable bindings (captured empirically), not hand-written phrases —
    // the previous synthetic strings passed while production silently did not downgrade.

    @Test
    @DisplayName("'ValueSet <url> not found' (Terminology_TX_ValueSet_NotFound) becomes not-evaluated, not an error")
    void valueSetNotFoundMessageBecomesNotEvaluated() {
        stubValidator(
                message(ResultSeverityEnum.WARNING,
                        "ValueSet 'http://nhsn/vs' not found",
                        "Terminology_TX_ValueSet_NotFound"),
                message(ResultSeverityEnum.ERROR, "Patient.name: minimum required = 1, but only found 0"));

        List<RawFinding> findings = executor.execute(check(), context());

        assertThat(findings).hasSize(2);

        RawFinding notEvaluated = findings.get(0);
        assertThat(notEvaluated.getCode()).isEqualTo("binding-not-evaluated");
        assertThat(notEvaluated.getSeverity()).isEqualTo(Severity.INFORMATION);
        assertThat(notEvaluated.isNotEvaluated()).isTrue();

        RawFinding realError = findings.get(1);
        assertThat(realError.getCode()).isEqualTo("fhir-conformance");
        assertThat(realError.getSeverity()).isEqualTo(Severity.ERROR);
        assertThat(realError.isNotEvaluated()).isFalse();
    }

    @Test
    @DisplayName("'CodeSystem is unknown and can't be validated' (Terminology_PassThrough) becomes not-evaluated")
    void unknownCodeSystemMessageBecomesNotEvaluated() {
        stubValidator(message(ResultSeverityEnum.WARNING,
                "CodeSystem is unknown and can't be validated: http://loinc.org for 'http://loinc.org#1234-5'",
                "Terminology_PassThrough_TX_Message"));

        List<RawFinding> findings = executor.execute(check(), context());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("binding-not-evaluated");
        assertThat(findings.get(0).isNotEvaluated()).isTrue();
    }

    @Test
    @DisplayName("a membership error co-located with an unresolvable-VS failure is downgraded (consequence, not a real miss)")
    void membershipErrorCoLocatedWithResolutionFailureIsDowngraded() {
        // Real HAPI trio when a required binding's value set references an unloaded code system: two
        // resolution failures plus a membership error, all at the same element path. All three are the
        // one env gap and must be not-evaluated, not three ERRORs.
        stubValidator(
                messageAt(ResultSeverityEnum.ERROR,
                        "Unable to expand ValueSet because CodeSystem could not be found: http://example.org/CodeSystem/unknown-cs",
                        "Terminology_PassThrough_TX_Message", "Observation.code"),
                messageAt(ResultSeverityEnum.ERROR,
                        "CodeSystem is unknown and can't be validated: http://example.org/CodeSystem/unknown-cs for 'http://example.org/CodeSystem/unknown-cs#abc'",
                        "Terminology_PassThrough_TX_Message", "Observation.code"),
                messageAt(ResultSeverityEnum.ERROR,
                        "None of the codings provided are in the value set 'ValueSet[http://x/vs]' (http://x/vs), and a coding from this value set is required) (codes = http://example.org/CodeSystem/unknown-cs#abc)",
                        "Terminology_TX_NoValid_1_CC", "Observation.code"));

        List<RawFinding> findings = executor.execute(check(), context());

        assertThat(findings).hasSize(3);
        assertThat(findings).allSatisfy(f -> {
            assertThat(f.getCode()).isEqualTo("binding-not-evaluated");
            assertThat(f.isNotEvaluated()).isTrue();
        });
    }

    @Test
    @DisplayName("a genuine membership failure with NO co-located resolution failure stays an error")
    void genuineMembershipFailureWithoutResolutionStaysError() {
        // The value set resolved and the code genuinely is not a member — a real data error, not an env gap.
        stubValidator(messageAt(ResultSeverityEnum.ERROR,
                "None of the codings provided are in the value set 'V3 ActCode' (http://x/vs), and a coding from this value set is required)",
                "Terminology_TX_NoValid_1_CC", "Observation.code"));

        List<RawFinding> findings = executor.execute(check(), context());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("fhir-conformance");
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.ERROR);
        assertThat(findings.get(0).isNotEvaluated()).isFalse();
    }

    @Test
    @DisplayName("a membership error at a DIFFERENT element than the resolution failure stays an error")
    void membershipErrorAtUnrelatedLocationStaysError() {
        // A resolution failure on Observation.code must not silence a genuine membership miss on
        // Observation.value — different elements, so the second is a real data error.
        stubValidator(
                messageAt(ResultSeverityEnum.WARNING, "ValueSet 'http://x/vs' not found",
                        "Terminology_TX_ValueSet_NotFound", "Observation.code"),
                messageAt(ResultSeverityEnum.ERROR,
                        "None of the codings provided are in the value set 'http://y/vs', and a coding from this value set is required)",
                        "Terminology_TX_NoValid_1_CC", "Observation.value"));

        List<RawFinding> findings = executor.execute(check(), context());

        assertThat(findings).hasSize(2);
        assertThat(findings.get(0).isNotEvaluated()).isTrue();          // resolution failure on Observation.code
        assertThat(findings.get(1).getCode()).isEqualTo("fhir-conformance");
        assertThat(findings.get(1).getSeverity()).isEqualTo(Severity.ERROR); // genuine miss on Observation.value
        assertThat(findings.get(1).isNotEvaluated()).isFalse();
    }

    @Test
    @DisplayName("a membership error under a descendant path of the resolution failure IS downgraded")
    void membershipErrorAtDescendantLocationIsDowngraded() {
        // HAPI sometimes reports the per-coding message one level deeper (Observation.code.coding[0]).
        stubValidator(
                messageAt(ResultSeverityEnum.ERROR, "ValueSet 'http://x/vs' not found",
                        "Terminology_TX_ValueSet_NotFound", "Observation.code"),
                messageAt(ResultSeverityEnum.ERROR,
                        "None of the codings provided are in the value set 'http://x/vs', and a coding from this value set is required)",
                        "Terminology_TX_NoValid_1_CC", "Observation.code.coding[0]"));

        List<RawFinding> findings = executor.execute(check(), context());

        assertThat(findings).hasSize(2);
        assertThat(findings).allSatisfy(f -> assertThat(f.isNotEvaluated()).isTrue());
    }

    @Test
    @DisplayName("a structural (non-terminology) message is never reclassified, even with a resolution-like phrase")
    void structuralMessageWithResolutionPhraseIsNotReclassified() {
        // Non-terminology message id + a phrase that would match the marker list: must stay an error.
        stubValidator(message(ResultSeverityEnum.ERROR,
                "Extension http://x/ext could not be found", "Extension_EXT_Unknown"));

        List<RawFinding> findings = executor.execute(check(), context());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("fhir-conformance");
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.ERROR);
        assertThat(findings.get(0).isNotEvaluated()).isFalse();
    }
}
