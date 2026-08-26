package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.LegacyShadowFinding;
import com.lantanagroup.link.validation.entities.LegacyShadowResult;
import com.lantanagroup.link.validation.models.LegacyShadowResultDto;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import com.lantanagroup.link.validation.repositories.LegacyShadowFindingRepository;
import com.lantanagroup.link.validation.repositories.LegacyShadowResultRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

/**
 * Reads the legacy engine's result for a rubric request id, like {@link RubricResultQueryService} does
 * for the modern engine.
 */
@Service
@RequiredArgsConstructor
@Slf4j
public class LegacyShadowResultQueryService {

    private final LegacyShadowResultRepository legacyShadowResultRepository;
    private final LegacyShadowFindingRepository legacyShadowFindingRepository;
    private final ObjectMapper objectMapper;

    @Transactional(readOnly = true)
    public Optional<LegacyShadowResultDto> findByRequestId(UUID requestId) {
        return legacyShadowResultRepository.findFirstByRequestIdOrderByRequestedAtDesc(requestId)
                .map(this::toDto);
    }

    private LegacyShadowResultDto toDto(LegacyShadowResult r) {
        List<ShadowFindingDto> findings = legacyShadowFindingRepository.findByRequestId(r.getRequestId()).stream()
                .map(this::toFindingDto)
                .toList();

        return LegacyShadowResultDto.builder()
                .resultId(r.getResultId())
                .requestId(r.getRequestId())
                .correlationId(r.getCorrelationId())
                .facilityId(r.getFacilityId())
                .patientId(r.getPatientId())
                .reportId(r.getReportId())
                .fatalCount(r.getFatalCount())
                .errorCount(r.getErrorCount())
                .warningCount(r.getWarningCount())
                .informationCount(r.getInformationCount())
                .findings(findings)
                .requestedAt(r.getRequestedAt())
                .completedAt(r.getCompletedAt())
                .durationMs(r.getDurationMs())
                .build();
    }

    private ShadowFindingDto toFindingDto(LegacyShadowFinding f) {
        return ShadowFindingDto.builder()
                .severity(f.getSeverity())
                .code(f.getCode())
                .message(f.getMessage())
                .location(f.getLocation())
                .expression(f.getExpression())
                .categoryIds(parseCategoryIds(f.getCategoryIdsJson(), f.getFindingId()))
                .acceptable(f.getAcceptable())
                .build();
    }

    private List<String> parseCategoryIds(String json, UUID findingId) {
        if (json == null || json.isBlank()) {
            return List.of();
        }
        try {
            return objectMapper.readValue(json, new TypeReference<List<String>>() {
            });
        } catch (Exception e) {
            log.warn("Failed to parse legacy shadow finding category ids for finding {}", findingId, e);
            return List.of();
        }
    }
}
