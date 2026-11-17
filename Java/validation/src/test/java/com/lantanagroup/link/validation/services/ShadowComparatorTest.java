package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.lantanagroup.link.validation.repositories.ShadowComparisonResultRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.verify;

@ExtendWith(MockitoExtension.class)
class ShadowComparatorTest {

    private static final UUID REQUEST_ID = UUID.randomUUID();

    @Mock
    private ShadowComparisonResultRepository shadowComparisonResultRepository;

    private final ObjectMapper objectMapper = new ObjectMapper();

    private ShadowComparator comparator;

    private static Result result(OperationOutcome.IssueSeverity severity, String location) {
        Result result = new Result();
        result.setSeverity(severity);
        result.setCode(OperationOutcome.IssueType.INVALID);
        result.setLocation(location);
        result.setExpression(location);
        return result;
    }

    @Test
    void matchingResultsAreLoggedAsMatchAndPersistedAsMatched() {
        comparator = new ShadowComparator(shadowComparisonResultRepository, objectMapper);
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, "loc");
        Result modern = result(OperationOutcome.IssueSeverity.ERROR, "loc");

        comparator.compareAndLog("corr-1", REQUEST_ID, "facility-1", "patient-1", "report-1", "rubric-1",
                true, List.of(legacy), List.of(modern));

        ArgumentCaptor<ShadowComparisonResult> captor = ArgumentCaptor.forClass(ShadowComparisonResult.class);
        verify(shadowComparisonResultRepository).save(captor.capture());
        ShadowComparisonResult saved = captor.getValue();
        assertEquals(REQUEST_ID, saved.getRequestId());
        assertTrue(saved.isMatched());
        assertEquals(0, saved.getAddedCount());
        assertEquals(0, saved.getMissingCount());
        assertEquals(0, saved.getSeverityChangedCount());
        assertNull(saved.getAddedJson());
        assertNull(saved.getMissingJson());
        assertNull(saved.getSeverityChangedJson());
        assertEquals(1, saved.getMatchedFindingCount());
        assertEquals("corr-1", saved.getCorrelationId());
        assertEquals("facility-1", saved.getFacilityId());
        assertEquals("patient-1", saved.getPatientId());
        assertEquals("report-1", saved.getReportId());
        assertEquals("rubric-1", saved.getRubricId());
        assertTrue(saved.isRanNewEngine());
    }

    @Test
    void differingResultsAreLoggedAsDiffAndPersistedWithCounts() {
        comparator = new ShadowComparator(shadowComparisonResultRepository, objectMapper);
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, "loc");
        Result modernExtra = result(OperationOutcome.IssueSeverity.WARNING, "other-loc");

        comparator.compareAndLog("corr-2", REQUEST_ID, "facility-1", "patient-1", "report-2", "rubric-1",
                false, List.of(legacy), List.of(modernExtra));

        ArgumentCaptor<ShadowComparisonResult> captor = ArgumentCaptor.forClass(ShadowComparisonResult.class);
        verify(shadowComparisonResultRepository).save(captor.capture());
        ShadowComparisonResult saved = captor.getValue();
        assertEquals(REQUEST_ID, saved.getRequestId());
        assertFalse(saved.isMatched());
        assertEquals(1, saved.getAddedCount());
        assertEquals(1, saved.getMissingCount());
        assertEquals(0, saved.getSeverityChangedCount());
        assertEquals(0, saved.getMatchedFindingCount());
        assertFalse(saved.isRanNewEngine());
        assertTrue(saved.getAddedJson().contains("\"other-loc\""), "added findings json should list the new-only finding");
        assertTrue(saved.getMissingJson().contains("\"loc\""), "missing findings json should list the legacy-only finding");
        assertNull(saved.getSeverityChangedJson());
    }
}
