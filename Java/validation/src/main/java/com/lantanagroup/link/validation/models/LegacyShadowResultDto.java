package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

/**
 * ADR-0003 shadow-run: response shape for the legacy engine's run against a rubric request id, alongside
 * {@link ShadowComparisonResultDto} in {@link ShadowComparisonDetailDto}. Remove alongside the rest of the
 * temporary shadow-run read side once ADR-0003 cuts over.
 */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class LegacyShadowResultDto {
    private UUID resultId;
    private UUID requestId;
    private String correlationId;
    private String facilityId;
    private String patientId;
    private String reportId;
    private int fatalCount;
    private int errorCount;
    private int warningCount;
    private int informationCount;
    private List<ShadowFindingDto> findings;
    private OffsetDateTime requestedAt;
    private OffsetDateTime completedAt;
    private long durationMs;
}
