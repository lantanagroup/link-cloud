package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.ValidationPolicyConfig;
import com.lantanagroup.link.validation.entities.RubricFinding;
import com.lantanagroup.link.validation.entities.RubricResult;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.*;
import com.lantanagroup.link.validation.services.execution.CheckExecutionResult;
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;
import com.lantanagroup.link.validation.services.scoring.ScoringPolicyResolver;
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
    private final ScoringPolicyResolver scoringPolicyResolver;
    private final ValidationPolicyConfig policyConfig;

    public AssembleOutput assemble(
            ExecutionContext ctx,
            RubricVersion version,
            List<EvaluatedFinding> evaluated,
            List<CheckExecutionResult> checkResults,
            Map<String, Long> checkDurationsMs,
            OffsetDateTime completedAt) {

        long durationMs = completedAt.toInstant().toEpochMilli() - ctx.getRequestedAt().toInstant().toEpochMilli();

        long checkWorkMs = checkDurationsMs == null ? 0L
                : checkDurationsMs.values().stream().filter(Objects::nonNull).mapToLong(Long::longValue).sum();

        ScoringPolicyDto scoringPolicy = scoringPolicyResolver.resolve(version.getScoringPolicyJson());
        ScoreCardDto score = scoreAggregator.aggregate(checkResults, scoringPolicy);

        // Summary counts stay on the pre-override severities so the summary reconciles with what
        // the checks actually emitted; the override's effect shows in the score and per finding.
        SummaryDto summary = SummaryDto.builder()
                .errorCount(countBy(evaluated, Severity.ERROR))
                .warningCount(countBy(evaluated, Severity.WARNING))
                .informationCount(countBy(evaluated, Severity.INFORMATION))
                .build();

        UUID resultId = UUID.randomUUID();
        ValidationPolicyConfig.Response responseConfig = policyConfig.getResponse();

        List<FindingDto> findingDtos = new ArrayList<>(evaluated.size());
        List<RubricFinding> findingEntities = new ArrayList<>(evaluated.size());
        for (EvaluatedFinding f : evaluated) {
            RawFinding r = f.raw();
            UUID fid = UUID.randomUUID();
            findingDtos.add(toDto(fid, f, r, responseConfig));
            findingEntities.add(RubricFinding.builder()
                    .findingId(fid)
                    .resultId(resultId)
                    .checkId(r.getCheckId())
                    .dimension(r.getDimension())
                    // Effective severity, so persisted findings reconcile with the persisted status.
                    // The pre-override severity is reported in the response but not stored; keeping
                    // both would need a schema change.
                    .severity(f.effectiveSeverity())
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

    private FindingDto toDto(UUID findingId, EvaluatedFinding f, RawFinding r,
                             ValidationPolicyConfig.Response responseConfig) {
        FindingDto.FindingDtoBuilder builder = FindingDto.builder()
                .id(findingId)
                .checkId(r.getCheckLocalId())
                .dimension(r.getDimension())
                .severity(f.effectiveSeverity())
                .code(r.getCode())
                .message(r.getMessage())
                .location(r.getLocation())
                .expression(r.getExpression());

        if (!f.hasCategories()) {
            return builder.build();
        }
        builder.acceptable(f.acceptable());
        if (responseConfig.isIncludeOriginalSeverity()) {
            builder.originalSeverity(f.originalSeverity());
            if (f.severityWasOverridden()) {
                builder.overriddenSeverity(f.effectiveSeverity());
            }
        }
        if (responseConfig.isIncludeCategoryIds()) {
            builder.categoryIds(f.categoryIds());
            builder.governingCategoryId(f.governingCategoryId());
        }
        return builder.build();
    }

    private int countBy(List<EvaluatedFinding> findings, Severity sev) {
        return (int) findings.stream().filter(f -> f.originalSeverity() == sev).count();
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
