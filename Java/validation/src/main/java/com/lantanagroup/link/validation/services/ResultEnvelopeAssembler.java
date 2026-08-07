package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricFinding;
import com.lantanagroup.link.validation.entities.RubricResult;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.*;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;
import java.util.*;

@Component
@RequiredArgsConstructor
@Slf4j
public class ResultEnvelopeAssembler {

    private final ObjectMapper objectMapper;
    private final ScoreAggregator scoreAggregator;

    public AssembleOutput assemble(
            ExecutionContext ctx,
            RubricVersion version,
            List<RawFinding> raw,
            Map<String, Long> checkDurationsMs,
            OffsetDateTime completedAt) {

        long durationMs = completedAt.toInstant().toEpochMilli() - ctx.getRequestedAt().toInstant().toEpochMilli();

        long checkWorkMs = checkDurationsMs == null ? 0L
                : checkDurationsMs.values().stream().filter(Objects::nonNull).mapToLong(Long::longValue).sum();

        ScoreCardDto score = scoreAggregator.aggregate(raw);

        SummaryDto summary = SummaryDto.builder()
                .errorCount(countBy(raw, Severity.ERROR))
                .warningCount(countBy(raw, Severity.WARNING))
                .informationCount(countBy(raw, Severity.INFORMATION))
                .build();

        UUID resultId = UUID.randomUUID();

        List<FindingDto> findingDtos = new ArrayList<>(raw.size());
        List<RubricFinding> findingEntities = new ArrayList<>(raw.size());
        for (RawFinding r : raw) {
            UUID fid = UUID.randomUUID();
            findingDtos.add(FindingDto.builder()
                    .id(fid)
                    .checkId(r.getCheckLocalId())
                    .dimension(r.getDimension())
                    .severity(r.getSeverity())
                    .code(r.getCode())
                    .message(r.getMessage())
                    .location(r.getLocation())
                    .expression(r.getExpression())
                    .build());
            findingEntities.add(RubricFinding.builder()
                    .findingId(fid)
                    .resultId(resultId)
                    .checkId(r.getCheckId())
                    .dimension(r.getDimension())
                    .severity(r.getSeverity())
                    .code(r.getCode())
                    .message(r.getMessage())
                    .location(r.getLocation())
                    .expression(r.getExpression())
                    .build());
        }

        ValidationResultEnvelope envelope = ValidationResultEnvelope.builder()
                .requestId(ctx.getRequestId())
                .correlationId(ctx.getCorrelationId())
                .rubricId(version.getRubricId())
                .rubricVersion(version.getSemver())
                .rubricVersionHash(version.getChecksum())
                .subject(ctx.getSubject())
                .status(score.getInterpretation())
                .score(score)
                .summary(summary)
                .findings(findingDtos)
                .supportingArtifacts(List.of())
                .trace(TraceDto.builder()
                        .requestedAt(ctx.getRequestedAt())
                        .completedAt(completedAt)
                        .durationMs(durationMs)
                        .checkDurationsMs(checkDurationsMs)
                        .checkWorkMs(checkWorkMs)
                        .validatorVersion("vaas-0.2.0")
                        .build())
                .build();

        RubricResult resultEntity = RubricResult.builder()
                .resultId(resultId)
                .requestId(ctx.getRequestId())
                .rubricId(version.getRubricId())
                .rubricVersionId(version.getRubricVersionId())
                .status(score.getInterpretation())
                .scoreJson(writeJson(score))
                .errorCount(summary.getErrorCount())
                .warningCount(summary.getWarningCount())
                .informationCount(summary.getInformationCount())
                .correlationId(ctx.getCorrelationId())
                .requestor(ctx.getRequestor())
                .facilityId(ctx.getSubject() != null ? ctx.getSubject().getFacilityId() : null)
                .patientId(ctx.getSubject() != null ? ctx.getSubject().getPatientId() : null)
                .reportId(ctx.getSubject() != null ? ctx.getSubject().getReportId() : null)
                .workflowTag(ctx.getSubject() != null ? ctx.getSubject().getWorkflow() : null)
                .stage(ctx.getSubject() != null ? ctx.getSubject().getStage() : null)
                .requestedAt(ctx.getRequestedAt())
                .completedAt(completedAt)
                .durationMs(durationMs)
                .build();

        return new AssembleOutput(envelope, resultEntity, findingEntities);
    }

    private int countBy(List<RawFinding> findings, Severity sev) {
        return (int) findings.stream().filter(f -> f.getSeverity() == sev).count();
    }

    private String writeJson(Object value) {
        try {
            return objectMapper.writeValueAsString(value);
        } catch (Exception e) {
            log.error("Failed to write JSON for {}", value.getClass().getSimpleName(), e);
            return null;
        }
    }

    public record AssembleOutput(
            ValidationResultEnvelope envelope,
            RubricResult resultEntity,
            List<RubricFinding> findingEntities) {}
}
