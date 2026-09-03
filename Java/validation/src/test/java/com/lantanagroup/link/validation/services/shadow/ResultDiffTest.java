package com.lantanagroup.link.validation.services.shadow;

import com.lantanagroup.link.validation.entities.Result;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertTrue;

class ResultDiffTest {

    /** Message (and patientId) default to null -- the matching key is (patientId, expression), with
     *  severity and message compared as the "value" once a key match is found, so callers that don't
     *  care about message can keep using this 4-arg form unchanged. */
    private static Result result(OperationOutcome.IssueSeverity severity, OperationOutcome.IssueType code,
                                  String location, String expression) {
        return result(severity, code, location, expression, null);
    }

    private static Result result(OperationOutcome.IssueSeverity severity, OperationOutcome.IssueType code,
                                  String location, String expression, String message) {
        return result(severity, code, location, expression, message, null);
    }

    private static Result result(OperationOutcome.IssueSeverity severity, OperationOutcome.IssueType code,
                                  String location, String expression, String message, String patientId) {
        Result result = new Result();
        result.setSeverity(severity);
        result.setCode(code);
        result.setLocation(location);
        result.setExpression(expression);
        result.setMessage(message);
        result.setPatientId(patientId);
        return result;
    }

    @Test
    void identicalListsAreEmptyDiff() {
        Result a = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr");
        Result b = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr");

        ResultDiff diff = ResultDiff.between(List.of(a), List.of(b));

        assertTrue(diff.isEmpty());
        assertEquals(1, diff.getMatchedCount());
        assertEquals(1, diff.getMatched().size());
        assertSame(a, diff.getMatched().get(0).legacy());
        assertSame(b, diff.getMatched().get(0).modern());
        assertEquals(0, diff.getAdded().size());
        assertEquals(0, diff.getMissing().size());
        assertEquals(0, diff.getSeverityChanged().size());
    }

    @Test
    void newOnlyFindingIsAdded() {
        Result newOnly = result(OperationOutcome.IssueSeverity.WARNING, OperationOutcome.IssueType.INVALID, "loc", "expr");

        ResultDiff diff = ResultDiff.between(List.of(), List.of(newOnly));

        assertEquals(List.of(newOnly), diff.getAdded());
        assertTrue(diff.getMissing().isEmpty());
        assertTrue(diff.getSeverityChanged().isEmpty());
        assertEquals(0, diff.getMatchedCount());
        assertFalse(diff.isEmpty());
    }

    @Test
    void legacyOnlyFindingIsMissing() {
        Result legacyOnly = result(OperationOutcome.IssueSeverity.WARNING, OperationOutcome.IssueType.INVALID, "loc", "expr");

        ResultDiff diff = ResultDiff.between(List.of(legacyOnly), List.of());

        assertEquals(List.of(legacyOnly), diff.getMissing());
        assertTrue(diff.getAdded().isEmpty());
        assertTrue(diff.getSeverityChanged().isEmpty());
        assertEquals(0, diff.getMatchedCount());
    }

    @Test
    void sameKeyDifferentSeverityIsSeverityChanged() {
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr");
        Result modern = result(OperationOutcome.IssueSeverity.WARNING, OperationOutcome.IssueType.INVALID, "loc", "expr");

        ResultDiff diff = ResultDiff.between(List.of(legacy), List.of(modern));

        assertEquals(1, diff.getSeverityChanged().size());
        assertSame(legacy, diff.getSeverityChanged().get(0).legacy());
        assertSame(modern, diff.getSeverityChanged().get(0).modern());
        assertEquals(0, diff.getMatchedCount());
        assertTrue(diff.getAdded().isEmpty());
        assertTrue(diff.getMissing().isEmpty());
    }

    @Test
    void duplicateKeysAreMatchedPositionallyNotCollapsed() {
        Result legacy1 = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr");
        Result legacy2 = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr");
        Result modern1 = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr");

        // two identical legacy findings, only one modern match -> one matched, one still missing
        ResultDiff diff = ResultDiff.between(List.of(legacy1, legacy2), List.of(modern1));

        assertEquals(1, diff.getMatchedCount());
        assertEquals(1, diff.getMatched().size());
        assertSame(legacy1, diff.getMatched().get(0).legacy());
        assertSame(modern1, diff.getMatched().get(0).modern());
        assertEquals(1, diff.getMissing().size());
        assertTrue(diff.getAdded().isEmpty());
    }

    @Test
    void nullExpressionAndMessageDoNotThrow() {
        Result a = result(OperationOutcome.IssueSeverity.INFORMATION, OperationOutcome.IssueType.NULL, null, null, null);
        Result b = result(OperationOutcome.IssueSeverity.INFORMATION, OperationOutcome.IssueType.NULL, null, null, null);

        ResultDiff diff = ResultDiff.between(List.of(a), List.of(b));

        assertTrue(diff.isEmpty());
    }

    @Test
    void sameExpressionDifferentLocationAndCodeStillMatches() {
        // location and code are not part of the key or the value comparison -- only expression decides
        // candidacy, and severity + message (both equal here) decide the match.
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID,
                "loc-a", "expr", "same message");
        Result modern = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.STRUCTURE,
                "loc-b", "expr", "same message");

        ResultDiff diff = ResultDiff.between(List.of(legacy), List.of(modern));

        assertTrue(diff.isEmpty());
        assertEquals(1, diff.getMatchedCount());
        assertEquals(1, diff.getMatched().size());
        assertSame(legacy, diff.getMatched().get(0).legacy());
        assertSame(modern, diff.getMatched().get(0).modern());
    }

    @Test
    void samePatientSameExpressionAndMessageDifferentSeverityIsSeverityChanged() {
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID,
                "loc", "expr", "same message", "patient-1");
        Result modern = result(OperationOutcome.IssueSeverity.WARNING, OperationOutcome.IssueType.INVALID,
                "loc", "expr", "same message", "patient-1");

        ResultDiff diff = ResultDiff.between(List.of(legacy), List.of(modern));

        assertEquals(1, diff.getSeverityChanged().size());
        assertEquals(0, diff.getMatchedCount());
    }

    @Test
    void sameSeverityExpressionAndMessageDifferentPatientIdDoesNotMatch() {
        // same severity/expression/message but a different patientId -- patientId is part of the key,
        // so no candidate is even found and this is added+missing, not matched or severity-changed.
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID,
                "loc", "expr", "same message", "patient-1");
        Result modern = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID,
                "loc", "expr", "same message", "patient-2");

        ResultDiff diff = ResultDiff.between(List.of(legacy), List.of(modern));

        assertEquals(List.of(modern), diff.getAdded());
        assertEquals(List.of(legacy), diff.getMissing());
        assertTrue(diff.getSeverityChanged().isEmpty());
        assertEquals(0, diff.getMatchedCount());
    }

    @Test
    void sameKeyDifferentMessageSameSeverityIsSeverityChanged() {
        // same (patientId, expression) key and same severity, but a different message -- message is
        // part of the value comparison now, so a mismatch here still falls short of "matched" and is
        // reported alongside severity mismatches rather than as added+missing.
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID,
                "loc", "expr", "legacy wording");
        Result modern = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID,
                "loc", "expr", "different wording");

        ResultDiff diff = ResultDiff.between(List.of(legacy), List.of(modern));

        assertEquals(1, diff.getSeverityChanged().size());
        assertSame(legacy, diff.getSeverityChanged().get(0).legacy());
        assertSame(modern, diff.getSeverityChanged().get(0).modern());
        assertTrue(diff.getAdded().isEmpty());
        assertTrue(diff.getMissing().isEmpty());
        assertEquals(0, diff.getMatchedCount());
    }

    @Test
    void messageDifferingOnlyByObjectIdentityHashIsMatched() {
        // both messages embed a default Object#toString() identity hash (class@hexhash) -- a JVM-run
        // artifact that differs between the legacy and modern engine's own runs even for the same
        // underlying finding, and must be normalized away before matching.
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr",
                "Unable to validate code against ValueSet$ConceptSetFilterComponent@5a0bb980 -- no matching code found");
        Result modern = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr",
                "Unable to validate code against ValueSet$ConceptSetFilterComponent@568cfa8f -- no matching code found");

        ResultDiff diff = ResultDiff.between(List.of(legacy), List.of(modern));

        assertTrue(diff.isEmpty());
        assertEquals(1, diff.getMatchedCount());
        assertSame(legacy, diff.getMatched().get(0).legacy());
        assertSame(modern, diff.getMatched().get(0).modern());
        assertTrue(diff.getAdded().isEmpty());
        assertTrue(diff.getMissing().isEmpty());
    }

    @Test
    void messagesDifferingBeyondTheObjectIdentityHashAreNotMatched() {
        // the hash normalization must not swallow real content differences -- here the class names
        // themselves differ (not just the trailing hash), so the normalized messages still differ.
        // Same (patientId, expression) key and same severity, so this is a message value mismatch
        // reported as a severity change rather than a match -- not added+missing (the key still lines up).
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr",
                "Unable to validate code against ValueSet$ConceptSetFilterComponent@5a0bb980 -- no matching code found");
        Result modern = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "loc", "expr",
                "Unable to validate code against CodeSystem$ConceptDefinitionComponent@568cfa8f -- no matching code found");

        ResultDiff diff = ResultDiff.between(List.of(legacy), List.of(modern));

        assertEquals(1, diff.getSeverityChanged().size());
        assertSame(legacy, diff.getSeverityChanged().get(0).legacy());
        assertSame(modern, diff.getSeverityChanged().get(0).modern());
        assertTrue(diff.getAdded().isEmpty());
        assertTrue(diff.getMissing().isEmpty());
        assertEquals(0, diff.getMatchedCount());
    }

    @Test
    void summaryReportsAllFourCounts() {
        Result added = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "a", "a");
        Result missing = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "b", "b");
        Result changedLegacy = result(OperationOutcome.IssueSeverity.ERROR, OperationOutcome.IssueType.INVALID, "c", "c");
        Result changedModern = result(OperationOutcome.IssueSeverity.WARNING, OperationOutcome.IssueType.INVALID, "c", "c");
        Result matchedLegacy = result(OperationOutcome.IssueSeverity.WARNING, OperationOutcome.IssueType.INVALID, "d", "d");
        Result matchedModern = result(OperationOutcome.IssueSeverity.WARNING, OperationOutcome.IssueType.INVALID, "d", "d");

        ResultDiff diff = ResultDiff.between(
                List.of(missing, changedLegacy, matchedLegacy),
                List.of(added, changedModern, matchedModern));

        assertEquals("added=1 missing=1 severityChanged=1 matched=1", diff.summary());
        assertEquals(1, diff.getMatched().size());
        assertSame(matchedLegacy, diff.getMatched().get(0).legacy());
        assertSame(matchedModern, diff.getMatched().get(0).modern());
    }
}
