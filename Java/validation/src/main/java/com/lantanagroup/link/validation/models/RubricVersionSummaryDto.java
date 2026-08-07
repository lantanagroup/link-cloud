package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import lombok.Builder;
import lombok.Getter;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

@Getter
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RubricVersionSummaryDto {

    private UUID rubricVersionId;
    private String rubricId;
    private String semver;
    private RubricVersionStatus status;
    private String checksum;
    // Per-version declarative metadata (sourced from the rubric_version row, not the rubric).
    private List<PiqiDimension> dimensions;
    private JsonNode applicableContext;
    private JsonNode scoringPolicy;
    private OffsetDateTime createdAt;
    private String createdBy;
    private OffsetDateTime publishedAt;
    private String publishedBy;
    private OffsetDateTime retiredAt;
    private String retiredBy;
    private OffsetDateTime dryRunCompletedAt;
    private RubricResultStatus dryRunStatus;

    public static RubricVersionSummaryDto from(RubricVersion version, ObjectMapper objectMapper) {
        return RubricVersionSummaryDto.builder()
                .rubricVersionId(version.getRubricVersionId())
                .rubricId(version.getRubricId())
                .semver(version.getSemver())
                .status(version.getStatus())
                .checksum(version.getChecksum())
                .dimensions(RubricSummaryDto.parseDimensions(version.getDimensionsJson(), objectMapper))
                .applicableContext(RubricDetailDto.parseNode(version.getApplicableContextJson(), objectMapper))
                .scoringPolicy(RubricDetailDto.parseNode(version.getScoringPolicyJson(), objectMapper))
                .createdAt(version.getCreatedAt())
                .createdBy(version.getCreatedBy())
                .publishedAt(version.getPublishedAt())
                .publishedBy(version.getPublishedBy())
                .retiredAt(version.getRetiredAt())
                .retiredBy(version.getRetiredBy())
                .dryRunCompletedAt(version.getDryRunCompletedAt())
                .dryRunStatus(version.getDryRunStatus())
                .build();
    }
}
