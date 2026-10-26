package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.lantanagroup.link.validation.models.ShadowComparisonResultDto;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import com.lantanagroup.link.validation.records.ShadowSeverityChangeDto;
import com.lantanagroup.link.validation.repositories.ShadowComparisonResultRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.UUID;

/**
 * Read side for the "fetch shadow comparisons by request id" API, so a request's diff can be inspected
 * without querying {@code shadow_comparison_result} directly.
 */
@Service
@RequiredArgsConstructor
@Slf4j
public class ShadowComparisonQueryService {

    private final ShadowComparisonResultRepository shadowComparisonResultRepository;
    private final ObjectMapper objectMapper;

    @Transactional(readOnly = true)
    public List<ShadowComparisonResultDto> findByRequestId(UUID requestId) {
        return shadowComparisonResultRepository.findByRequestIdOrderByComparedAtDesc(requestId).stream()
                .map(this::toDto)
                .toList();
    }

    private ShadowComparisonResultDto toDto(ShadowComparisonResult r) {
        return ShadowComparisonResultDto.builder()
                .id(r.getId())
                .requestId(r.getRequestId())
                .correlationId(r.getCorrelationId())
                .facilityId(r.getFacilityId())
                .patientId(r.getPatientId())
                .reportId(r.getReportId())
                .rubricId(r.getRubricId())
                .ranNewEngine(r.isRanNewEngine())
                .matched(r.isMatched())
                .addedCount(r.getAddedCount())
                .missingCount(r.getMissingCount())
                .severityChangedCount(r.getSeverityChangedCount())
                .matchedFindingCount(r.getMatchedFindingCount())
                .added(parseFindings(r.getAddedJson(), r.getId()))
                .missing(parseFindings(r.getMissingJson(), r.getId()))
                .severityChanged(parseSeverityChanges(r.getSeverityChangedJson(), r.getId()))
                .comparedAt(r.getComparedAt())
                .build();
    }

    private List<ShadowFindingDto> parseFindings(String json, UUID comparisonId) {
        if (json == null || json.isBlank()) {
            return List.of();
        }
        try {
            return objectMapper.readValue(json, new TypeReference<List<ShadowFindingDto>>() {
            });
        } catch (Exception e) {
            log.warn("Failed to parse shadow finding json for comparison {}", comparisonId, e);
            return List.of();
        }
    }

    private List<ShadowSeverityChangeDto> parseSeverityChanges(String json, UUID comparisonId) {
        if (json == null || json.isBlank()) {
            return List.of();
        }
        try {
            return objectMapper.readValue(json, new TypeReference<List<ShadowSeverityChangeDto>>() {
            });
        } catch (Exception e) {
            log.warn("Failed to parse shadow severity-changed json for comparison {}", comparisonId, e);
            return List.of();
        }
    }
}
