package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricFinding;
import com.lantanagroup.link.validation.entities.RubricResult;
import com.lantanagroup.link.validation.models.FindingDto;
import com.lantanagroup.link.validation.models.RubricResultDto;
import com.lantanagroup.link.validation.models.ScoreCardDto;
import com.lantanagroup.link.validation.models.SubjectDto;
import com.lantanagroup.link.validation.models.SummaryDto;
import com.lantanagroup.link.validation.models.TraceDto;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricFindingRepository;
import com.lantanagroup.link.validation.repositories.RubricResultRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import java.util.stream.Collectors;


@Service
@RequiredArgsConstructor
@Slf4j
public class RubricResultQueryService {

    private final RubricResultRepository rubricResultRepository;
    private final RubricFindingRepository rubricFindingRepository;
    private final RubricCheckRepository rubricCheckRepository;
    private final ObjectMapper objectMapper;

    @Transactional(readOnly = true)
    public Optional<RubricResultDto> findByRequestId(UUID requestId) {
        return rubricResultRepository.findByRequestId(requestId).map(this::toDto);
    }

    private RubricResultDto toDto(RubricResult r) {
        List<RubricFinding> findings = rubricFindingRepository.findByResultId(r.getResultId());
        Map<UUID, String> localIdByCheckId = resolveCheckLocalIds(findings);

        List<FindingDto> findingDtos = findings.stream()
                .map(f -> FindingDto.builder()
                        .id(f.getFindingId())
                        .checkId(localIdByCheckId.getOrDefault(f.getCheckId(),
                                f.getCheckId() != null ? f.getCheckId().toString() : null))
                        .dimension(f.getDimension())
                        .severity(f.getSeverity())
                        .code(f.getCode())
                        .message(f.getMessage())
                        .location(f.getLocation())
                        .expression(f.getExpression())
                        .build())
                .collect(Collectors.toList());

        return RubricResultDto.builder()
                .requestId(r.getRequestId())
                .resultId(r.getResultId())
                .rubricId(r.getRubricId())
                .rubricVersionId(r.getRubricVersionId())
                .correlationId(r.getCorrelationId())
                .requestor(r.getRequestor())
                .subject(subject(r))
                .payloadRef(r.getPayloadRef())
                .facilityOverrideId(r.getFacilityOverrideId())
                .status(r.getStatus())
                .score(parseScore(r))
                .summary(SummaryDto.builder()
                        .errorCount(r.getErrorCount())
                        .warningCount(r.getWarningCount())
                        .informationCount(r.getInformationCount())
                        .build())
                .findings(findingDtos)
                .trace(TraceDto.builder()
                        .requestedAt(r.getRequestedAt())
                        .completedAt(r.getCompletedAt())
                        .durationMs(r.getDurationMs())
                        .build())
                .build();
    }

    private Map<UUID, String> resolveCheckLocalIds(List<RubricFinding> findings) {
        List<UUID> checkIds = findings.stream()
                .map(RubricFinding::getCheckId)
                .filter(java.util.Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        if (checkIds.isEmpty()) {
            return Map.of();
        }
        return rubricCheckRepository.findAllById(checkIds).stream()
                .collect(Collectors.toMap(RubricCheck::getCheckId, RubricCheck::getCheckLocalId,
                        (a, b) -> a));
    }

    private SubjectDto subject(RubricResult r) {
        // reconstructed from the flat columns; null when nothing was captured, so we don't emit an empty {}
        if (r.getFacilityId() == null && r.getPatientId() == null && r.getReportId() == null
                && r.getWorkflowTag() == null && r.getStage() == null) {
            return null;
        }
        return SubjectDto.builder()
                .facilityId(r.getFacilityId())
                .patientId(r.getPatientId())
                .reportId(r.getReportId())
                .workflow(r.getWorkflowTag())
                .stage(r.getStage())
                .build();
    }

    private ScoreCardDto parseScore(RubricResult r) {
        if (r.getScoreJson() == null || r.getScoreJson().isBlank()) {
            return null;
        }
        try {
            return objectMapper.readValue(r.getScoreJson(), ScoreCardDto.class);
        } catch (Exception e) {
            log.warn("Failed to parse score_json for result {}: {}", r.getResultId(), e.getMessage());
            return null;
        }
    }
}
