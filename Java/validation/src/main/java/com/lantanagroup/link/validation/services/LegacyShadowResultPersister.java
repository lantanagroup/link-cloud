package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.LegacyShadowFinding;
import com.lantanagroup.link.validation.entities.LegacyShadowResult;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.repositories.LegacyShadowFindingRepository;
import com.lantanagroup.link.validation.repositories.LegacyShadowResultRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.time.Duration;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.stream.Collectors;

/**
 * Persists the legacy engine's output when it ran only for comparison, the same way
 * {@code RubricResultPersister} does for the modern engine.
 */
@Component
public class LegacyShadowResultPersister {

    private final LegacyShadowResultRepository legacyShadowResultRepository;
    private final LegacyShadowFindingRepository legacyShadowFindingRepository;
    private final ObjectMapper objectMapper;

    public LegacyShadowResultPersister(
            LegacyShadowResultRepository legacyShadowResultRepository,
            LegacyShadowFindingRepository legacyShadowFindingRepository,
            ObjectMapper objectMapper) {
        this.legacyShadowResultRepository = legacyShadowResultRepository;
        this.legacyShadowFindingRepository = legacyShadowFindingRepository;
        this.objectMapper = objectMapper;
    }

    @Transactional
    public UUID persist(
            UUID requestId, String correlationId, String facilityId, String patientId, String reportId,
            List<Result> results, OffsetDateTime requestedAt, OffsetDateTime completedAt) {
        Map<OperationOutcome.IssueSeverity, Long> bySeverity = results.stream()
                .collect(Collectors.groupingBy(Result::getSeverity, Collectors.counting()));

        LegacyShadowResult header = LegacyShadowResult.builder()
                .requestId(requestId)
                .correlationId(correlationId)
                .facilityId(facilityId)
                .patientId(patientId)
                .reportId(reportId)
                .fatalCount(count(bySeverity, OperationOutcome.IssueSeverity.FATAL))
                .errorCount(count(bySeverity, OperationOutcome.IssueSeverity.ERROR))
                .warningCount(count(bySeverity, OperationOutcome.IssueSeverity.WARNING))
                .informationCount(count(bySeverity, OperationOutcome.IssueSeverity.INFORMATION))
                .requestedAt(requestedAt)
                .completedAt(completedAt)
                .durationMs(Duration.between(requestedAt, completedAt).toMillis())
                .build();
        header = legacyShadowResultRepository.save(header);

        UUID resultId = header.getResultId();
        List<LegacyShadowFinding> findings = results.stream()
                .map(result -> toFinding(resultId, requestId, result))
                .toList();
        legacyShadowFindingRepository.saveAll(findings);
        return resultId;
    }

    private int count(Map<OperationOutcome.IssueSeverity, Long> bySeverity, OperationOutcome.IssueSeverity severity) {
        return bySeverity.getOrDefault(severity, 0L).intValue();
    }

    private LegacyShadowFinding toFinding(UUID resultId, UUID requestId, Result result) {
        List<Category> categories = result.getCategories();
        Boolean acceptable = (categories == null || categories.isEmpty())
                ? null
                : categories.stream().allMatch(Category::isAcceptable);
        return LegacyShadowFinding.builder()
                .resultId(resultId)
                .requestId(requestId)
                .severity(result.getSeverity())
                .code(result.getCode())
                .message(result.getMessage())
                .location(result.getLocation())
                .expression(result.getExpression())
                .categoryIdsJson(writeCategoryIds(categories))
                .acceptable(acceptable)
                .build();
    }

    private String writeCategoryIds(List<Category> categories) {
        if (categories == null || categories.isEmpty()) {
            return null;
        }
        try {
            return objectMapper.writeValueAsString(categories.stream().map(Category::getId).toList());
        } catch (Exception e) {
            return null;
        }
    }
}
