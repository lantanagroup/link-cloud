package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.LegacyShadowFinding;
import com.lantanagroup.link.validation.entities.LegacyShadowResult;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.repositories.LegacyShadowFindingRepository;
import com.lantanagroup.link.validation.repositories.LegacyShadowResultRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class LegacyShadowResultPersisterTest {

    @Mock
    private LegacyShadowResultRepository legacyShadowResultRepository;

    @Mock
    private LegacyShadowFindingRepository legacyShadowFindingRepository;

    private final ObjectMapper objectMapper = new ObjectMapper();

    private LegacyShadowResultPersister persister;

    private static Result result(OperationOutcome.IssueSeverity severity, List<Category> categories) {
        Result result = new Result();
        result.setSeverity(severity);
        result.setCode(OperationOutcome.IssueType.INVALID);
        result.setMessage("msg");
        result.setLocation("loc");
        result.setExpression("expr");
        result.setCategories(categories);
        return result;
    }

    private static Category category(String id, boolean acceptable) {
        Category category = new Category();
        category.setId(id);
        category.setAcceptable(acceptable);
        return category;
    }

    @Test
    void persistsHeaderWithCountsBySeverity() {
        persister = new LegacyShadowResultPersister(
                legacyShadowResultRepository, legacyShadowFindingRepository, objectMapper);
        when(legacyShadowResultRepository.save(any(LegacyShadowResult.class)))
                .thenAnswer(invocation -> {
                    LegacyShadowResult arg = invocation.getArgument(0);
                    arg.setResultId(UUID.randomUUID());
                    return arg;
                });

        List<Result> results = List.of(
                result(OperationOutcome.IssueSeverity.ERROR, List.of()),
                result(OperationOutcome.IssueSeverity.ERROR, List.of()),
                result(OperationOutcome.IssueSeverity.WARNING, List.of()));
        OffsetDateTime requestedAt = OffsetDateTime.now().minusSeconds(1);
        OffsetDateTime completedAt = OffsetDateTime.now();

        UUID requestId = UUID.randomUUID();
        UUID resultId = persister.persist(requestId, "corr-1", "facility-1", "patient-1", "report-1",
                results, requestedAt, completedAt);

        ArgumentCaptor<LegacyShadowResult> headerCaptor = ArgumentCaptor.forClass(LegacyShadowResult.class);
        verify(legacyShadowResultRepository).save(headerCaptor.capture());
        LegacyShadowResult header = headerCaptor.getValue();
        assertEquals(2, header.getErrorCount());
        assertEquals(1, header.getWarningCount());
        assertEquals(0, header.getFatalCount());
        assertEquals(0, header.getInformationCount());
        assertEquals(requestId, header.getRequestId());
        assertEquals("corr-1", header.getCorrelationId());
        assertEquals("facility-1", header.getFacilityId());
        assertEquals(resultId, header.getResultId());
    }

    @Test
    @SuppressWarnings("unchecked")
    void persistsOneFindingRowPerResultLinkedToTheHeader() {
        persister = new LegacyShadowResultPersister(
                legacyShadowResultRepository, legacyShadowFindingRepository, objectMapper);
        UUID fixedResultId = UUID.randomUUID();
        when(legacyShadowResultRepository.save(any(LegacyShadowResult.class)))
                .thenAnswer(invocation -> {
                    LegacyShadowResult arg = invocation.getArgument(0);
                    arg.setResultId(fixedResultId);
                    return arg;
                });

        Result withCategory = result(OperationOutcome.IssueSeverity.ERROR, List.of(category("cat-a", true)));
        Result uncategorized = result(OperationOutcome.IssueSeverity.WARNING, List.of());
        UUID requestId = UUID.randomUUID();

        persister.persist(requestId, "corr-1", "facility-1", "patient-1", "report-1",
                List.of(withCategory, uncategorized), OffsetDateTime.now(), OffsetDateTime.now());

        ArgumentCaptor<List<LegacyShadowFinding>> findingsCaptor = ArgumentCaptor.forClass(List.class);
        verify(legacyShadowFindingRepository).saveAll(findingsCaptor.capture());
        List<LegacyShadowFinding> findings = findingsCaptor.getValue();
        assertEquals(2, findings.size());
        assertTrue(findings.stream().allMatch(f -> fixedResultId.equals(f.getResultId())));
        assertTrue(findings.stream().allMatch(f -> requestId.equals(f.getRequestId())));

        LegacyShadowFinding categorized = findings.get(0);
        assertEquals(Boolean.TRUE, categorized.getAcceptable());
        assertTrue(categorized.getCategoryIdsJson().contains("cat-a"));

        LegacyShadowFinding notCategorized = findings.get(1);
        assertNull(notCategorized.getAcceptable());
    }
}
