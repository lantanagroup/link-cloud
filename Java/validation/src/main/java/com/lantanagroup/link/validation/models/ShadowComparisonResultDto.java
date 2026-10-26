package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import com.lantanagroup.link.validation.records.ShadowSeverityChangeDto;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

/**
 * A {@code ShadowComparisonResult} row with its JSON columns parsed into structured findings.
 */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class ShadowComparisonResultDto {
    private UUID id;
    private UUID requestId;
    private String correlationId;
    private String facilityId;
    private String patientId;
    private String reportId;
    private String rubricId;
    private boolean ranNewEngine;
    private boolean matched;
    private int addedCount;
    private int missingCount;
    private int severityChangedCount;
    private int matchedFindingCount;
    private List<ShadowFindingDto> added;
    private List<ShadowFindingDto> missing;
    private List<ShadowSeverityChangeDto> severityChanged;
    private OffsetDateTime comparedAt;
}
