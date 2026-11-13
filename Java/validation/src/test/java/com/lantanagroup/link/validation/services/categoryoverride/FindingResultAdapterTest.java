package com.lantanagroup.link.validation.services.categoryoverride;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.matchers.Matcher;
import com.lantanagroup.link.validation.models.RawFinding;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.NullAndEmptySource;
import org.junit.jupiter.params.provider.ValueSource;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Rubric check executors emit their own codes, none of which are FHIR IssueType codes, and there
 * are two ways that could turn into an error response: {@code IssueType.fromCode} throwing, and
 * persisting a Result whose non-nullable code column is null. The adapter closes both — it never
 * calls fromCode unguarded, and its Result is transient, never persisted.
 */
class FindingResultAdapterTest {

    /** Every code any executor in this service actually emits. */
    @ParameterizedTest
    @ValueSource(strings = {
            "fhir-conformance",
            "check-execution-error",
            "fhirpath-evaluation-error",
            "terminology-code-invalid",
            "valueset-membership-failed",
            "custom-check-not-found",
            "custom-check-error",
            "not-a-fhir-code-at-all",
            "Rule dom-6",
    })
    void noExecutorCodeCanThrow(String code) {
        assertDoesNotThrow(() -> IssueTypes.parseOrNull(code));
    }

    @Test
    void anUnknownCodeBecomesNullRatherThanAnException() {
        assertNull(IssueTypes.parseOrNull("fhir-conformance"));
        assertNull(IssueTypes.parseOrNull("not-a-fhir-code-at-all"));
    }

    @ParameterizedTest
    @NullAndEmptySource
    @ValueSource(strings = {"   "})
    void blankCodesBecomeNull(String code) {
        assertNull(IssueTypes.parseOrNull(code));
    }

    @Test
    void aRealFhirCodeStillResolves() {
        assertEquals(OperationOutcome.IssueType.CODEINVALID, IssueTypes.parseOrNull("code-invalid"));
    }

    /**
     * Without these aliases the one rule in categories.json that matches on CODE
     * (invalid_code_in_required_valueset, requiring code-invalid) could never match a rubric finding.
     */
    @Test
    void rubricCodesWithAnUnambiguousEquivalentAreAliased() {
        assertEquals(OperationOutcome.IssueType.CODEINVALID, IssueTypes.parseOrNull("terminology-code-invalid"));
        assertEquals(OperationOutcome.IssueType.CODEINVALID, IssueTypes.parseOrNull("valueset-membership-failed"));
        assertEquals(OperationOutcome.IssueType.PROCESSING, IssueTypes.parseOrNull("check-execution-error"));
    }

    /** Confirms the underlying call really does throw, so the try/catch is not decorative. */
    @Test
    void theUnguardedFhirCallWouldHaveThrown() {
        assertThrows(Exception.class, () -> OperationOutcome.IssueType.fromCode("fhir-conformance"));
    }

    // ------------------------------------------------------------------
    // Adapter
    // ------------------------------------------------------------------

    @Test
    void severityIsMappedWithoutFromCode() {
        assertEquals(OperationOutcome.IssueSeverity.ERROR, adapt(Severity.ERROR, "x").getSeverity());
        assertEquals(OperationOutcome.IssueSeverity.WARNING, adapt(Severity.WARNING, "x").getSeverity());
        assertEquals(OperationOutcome.IssueSeverity.INFORMATION, adapt(Severity.INFORMATION, "x").getSeverity());
        assertNull(adapt(null, "x").getSeverity());
    }

    @Test
    void messageAndExpressionCarryThroughForMatching() {
        Result result = adapt(Severity.ERROR, "fhir-conformance");

        assertEquals("some validator message", result.getMessage());
        assertEquals("Patient.name[0]", result.getExpression());
        assertNull(result.getCode(), "a rubric code has no FHIR IssueType");
    }

    /**
     * A null code must not break matching. This is the same contract CategoryController relies on
     * when it validates a submitted rule against a completely empty Result.
     */
    @Test
    void matchersStillWorkAgainstAnAdaptedFindingWithNoCode() throws Exception {
        Matcher matcher = new ObjectMapper().readValue("""
                {
                  "children": [
                    { "field": "MESSAGE", "regex": "validator message" },
                    { "field": "CODE", "regex": "^code-invalid$" }
                  ],
                  "requiresAllChildren": false
                }
                """, Matcher.class);
        Result result = adapt(Severity.ERROR, "fhir-conformance");

        assertTrue(assertDoesNotThrow(() -> matcher.isMatch(result)), "MESSAGE branch must still match");
    }

    @Test
    void aCodeOnlyRuleSimplyDoesNotMatchAnUnmappedCode() throws Exception {
        Matcher matcher = new ObjectMapper()
                .readValue("{ \"field\": \"CODE\", \"regex\": \"^code-invalid$\" }", Matcher.class);

        assertFalse(matcher.isMatch(adapt(Severity.ERROR, "fhir-conformance")));
        assertTrue(matcher.isMatch(adapt(Severity.ERROR, "terminology-code-invalid")), "via alias");
    }

    private static Result adapt(Severity severity, String code) {
        return FindingResultAdapter.toTransientResult(RawFinding.builder()
                .checkLocalId("c1")
                .dimension(PiqiDimension.CONFORMANCE)
                .severity(severity)
                .code(code)
                .message("some validator message")
                .location("Patient.name[0]")
                .expression("Patient.name[0]")
                .build());
    }
}
