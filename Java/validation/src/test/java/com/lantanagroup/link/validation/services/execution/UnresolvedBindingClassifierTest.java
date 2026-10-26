package com.lantanagroup.link.validation.services.execution;

import ca.uhn.fhir.context.support.IValidationSupport.CodeValidationIssue;
import ca.uhn.fhir.context.support.IValidationSupport.CodeValidationIssueCode;
import ca.uhn.fhir.context.support.IValidationSupport.CodeValidationIssueCoding;
import ca.uhn.fhir.context.support.IValidationSupport.CodeValidationResult;
import ca.uhn.fhir.context.support.IValidationSupport.IssueSeverity;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class UnresolvedBindingClassifierTest {

    // ------------------------------------------------------------------
    // Structured signal (primary): HAPI 8.x CodeValidationIssueCoding
    // ------------------------------------------------------------------

    private static CodeValidationResult resultWith(String message, CodeValidationIssueCoding... codings) {
        CodeValidationResult result = new CodeValidationResult();
        result.setMessage(message);
        List<CodeValidationIssue> issues = java.util.Arrays.stream(codings)
                .map(c -> new CodeValidationIssue("issue", IssueSeverity.ERROR,
                        c == CodeValidationIssueCoding.NOT_FOUND
                                ? CodeValidationIssueCode.NOT_FOUND
                                : CodeValidationIssueCode.CODE_INVALID,
                        c))
                .toList();
        result.setCodeValidationIssues(issues);
        return result;
    }

    @Test
    @DisplayName("NOT_FOUND coding -> unresolvable (not evaluated)")
    void notFoundCodingIsUnresolvable() {
        assertThat(UnresolvedBindingClassifier.isUnresolvable(
                resultWith("The code '9999-9' is not in the value set", CodeValidationIssueCoding.NOT_FOUND)))
                .as("structured NOT_FOUND wins even if the message looks like a membership failure")
                .isTrue();
    }

    @Test
    @DisplayName("NOT_IN_VS coding -> a genuine membership failure, NOT unresolvable")
    void notInVsCodingIsGenuine() {
        assertThat(UnresolvedBindingClassifier.isUnresolvable(
                resultWith("could not be resolved", CodeValidationIssueCoding.NOT_IN_VS)))
                .as("structured NOT_IN_VS wins even if the message contains a resolution marker")
                .isFalse();
    }

    @Test
    @DisplayName("INVALID_CODE coding -> genuine, NOT unresolvable")
    void invalidCodeCodingIsGenuine() {
        assertThat(UnresolvedBindingClassifier.isUnresolvable(
                resultWith(null, CodeValidationIssueCoding.INVALID_CODE))).isFalse();
    }

    @Test
    @DisplayName("a genuine issue alongside a NOT_FOUND is treated as genuine (not evaluated only when all are resolution failures)")
    void genuineWinsOverNotFound() {
        assertThat(UnresolvedBindingClassifier.isUnresolvable(
                resultWith(null, CodeValidationIssueCoding.NOT_FOUND, CodeValidationIssueCoding.NOT_IN_VS)))
                .isFalse();
    }

    @Test
    @DisplayName("no structured issues -> fall back to the message heuristic")
    void noIssuesFallsBackToMessage() {
        CodeValidationResult unresolved = new CodeValidationResult();
        unresolved.setMessage("Unable to expand ValueSet http://x/vs");
        assertThat(UnresolvedBindingClassifier.isUnresolvable(unresolved)).isTrue();

        CodeValidationResult genuine = new CodeValidationResult();
        genuine.setMessage("The code '9999-9' is not in the value set http://x/vs");
        assertThat(UnresolvedBindingClassifier.isUnresolvable(genuine)).isFalse();
    }

    @Test
    @DisplayName("a null result is not unresolvable")
    void nullResultIsNotUnresolvable() {
        assertThat(UnresolvedBindingClassifier.isUnresolvable((CodeValidationResult) null)).isFalse();
    }

    // ------------------------------------------------------------------
    // Message-text fallback (narrow, resource-resolution phrasings only)
    // ------------------------------------------------------------------

    @ParameterizedTest(name = "unresolvable: {0}")
    @ValueSource(strings = {
            "Unable to expand ValueSet http://x/vs",
            "The value set 'http://qicore/vs' could not be resolved",
            "Unable to resolve value set http://x/vs",
            "ValueSet http://x/vs was not found",
            "Unable to validate code 12345 because the value set http://nhsn/vs could not be found",
            "Error expanding ValueSet: dependency missing",
            // verbatim real HAPI 8.10 phrasings the previous marker list missed:
            "ValueSet 'http://example.org/ValueSet/does-not-exist' not found",
            "CodeSystem is unknown and can't be validated: http://loinc.org for 'http://loinc.org#1234-5'",
            "Unable to expand ValueSet because CodeSystem could not be found: http://x/cs",
    })
    @DisplayName("resource-resolution messages are classified as unresolvable")
    void resolutionFailuresAreUnresolvable(String message) {
        assertThat(UnresolvedBindingClassifier.isUnresolvable(message)).isTrue();
    }

    @ParameterizedTest(name = "NOT unresolvable: {0}")
    @ValueSource(strings = {
            // the dangerous ambiguous fragments that were dropped — these must NOT reclassify
            "Unable to validate code http://loinc.org#9999-9",
            "The code is not supported for this element",
            "The code system http://loinc.org is not known",
            // service-availability wording is a different failure mode (handled by exception/retry),
            // not a resource-resolution failure — the message classifier must not catch it
            "The terminology service is unavailable",
            // genuine membership / validity failures
            "The code '9999-9' is not in the value set http://x/vs",
            "None of the codings provided are in the value set http://x/vs",
            "Code '9999-9' (http://loinc.org) is not in value set http://x/vs",
            "The display 'Foo' is not valid for the code",
            // real HAPI membership wording — the code is the missing subject, the value set resolved:
            // these contain "was not found" but must NOT be treated as unresolvable
            "The value provided ('min') was not found in the value set 'UnitsOfTime' (http://hl7.org/fhir/ValueSet/units-of-time)",
            "The Coding provided (http://terminology.hl7.org/CodeSystem/v3-ActCode#WRKCOMP) was not found in the value set 'V3 Value Set ActEncounterCode'",
    })
    @DisplayName("ambiguous prefixes and genuine failures are NOT classified as unresolvable")
    void ambiguousAndGenuineFailuresAreNotUnresolvable(String message) {
        assertThat(UnresolvedBindingClassifier.isUnresolvable(message)).isFalse();
    }

    @Test
    @DisplayName("null or blank message is not unresolvable")
    void nullOrBlankIsNotUnresolvable() {
        assertThat(UnresolvedBindingClassifier.isUnresolvable((String) null)).isFalse();
        assertThat(UnresolvedBindingClassifier.isUnresolvable("")).isFalse();
        assertThat(UnresolvedBindingClassifier.isUnresolvable("   ")).isFalse();
    }

    // ------------------------------------------------------------------
    // Conformance-path message-id classification
    // ------------------------------------------------------------------

    @Test
    @DisplayName("the resolution message id wins even when the text carries no marker")
    void resolutionMessageIdWinsRegardlessOfText() {
        // The stable HAPI id classifies it even though the text ("... not found") is otherwise handled
        // by the marker fallback; and it holds even when the text is absent.
        assertThat(UnresolvedBindingClassifier.isUnresolvedConformanceMessage(
                "Terminology_TX_ValueSet_NotFound", "ValueSet 'http://x/vs' not found")).isTrue();
        assertThat(UnresolvedBindingClassifier.isUnresolvedConformanceMessage(
                "Terminology_TX_ValueSet_NotFound", null)).isTrue();
    }

    @Test
    @DisplayName("a non-resolution message id falls back to the text heuristic")
    void nonResolutionMessageIdFallsBackToText() {
        assertThat(UnresolvedBindingClassifier.isUnresolvedConformanceMessage(
                "Terminology_PassThrough_TX_Message",
                "CodeSystem is unknown and can't be validated: http://loinc.org")).isTrue();
        assertThat(UnresolvedBindingClassifier.isUnresolvedConformanceMessage(
                "Terminology_PassThrough_TX_Message",
                "The code '9999-9' is not in the value set http://x/vs")).isFalse();
    }

    @Test
    @DisplayName("membership failures are recognised by message id or by phrasing")
    void membershipFailureRecognisedByIdOrText() {
        assertThat(UnresolvedBindingClassifier.isMembershipFailure(
                "Terminology_TX_NoValid_1_CC", "anything at all")).isTrue();
        assertThat(UnresolvedBindingClassifier.isMembershipFailure(
                null, "None of the codings provided are in the value set 'X'")).isTrue();
        // a pure resolution failure is not a membership failure
        assertThat(UnresolvedBindingClassifier.isMembershipFailure(
                "Terminology_TX_ValueSet_NotFound", "ValueSet 'http://x/vs' not found")).isFalse();
    }
}
