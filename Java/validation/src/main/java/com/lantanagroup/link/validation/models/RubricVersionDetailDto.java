package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
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
public class RubricVersionDetailDto {

    private UUID rubricVersionId;
    private String rubricId;
    private String semver;
    private RubricVersionStatus status;
    private String checksum;
    private JsonNode definition;
    @JsonIgnore
    private List<CheckDto> checks;
    private OffsetDateTime createdAt;
    private String createdBy;
    private OffsetDateTime publishedAt;
    private String publishedBy;
    private OffsetDateTime retiredAt;
    private String retiredBy;
    private OffsetDateTime dryRunCompletedAt;
    private RubricResultStatus dryRunStatus;

    public static RubricVersionDetailDto from(RubricVersion version, List<RubricCheck> checks,
                                              ObjectMapper objectMapper) {
        return RubricVersionDetailDto.builder()
                .rubricVersionId(version.getRubricVersionId())
                .rubricId(version.getRubricId())
                .semver(version.getSemver())
                .status(version.getStatus())
                .checksum(version.getChecksum())
                .definition(RubricDetailDto.parseNode(version.getDefinitionJson(), objectMapper))
                .checks(checks.stream().map(c -> toCheckDto(c, objectMapper)).toList())
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

    private static CheckDto toCheckDto(RubricCheck check, ObjectMapper objectMapper) {
        return CheckDto.builder()
                .id(check.getCheckLocalId())
                .type(check.getType())
                .dimension(check.getDimension())
                .parameters(RubricDetailDto.parseNode(check.getParametersJson(), objectMapper))
                .severityOverride(check.getSeverityOverride())
                .ordinal(check.getOrdinal())
                .enabled(check.isEnabled())
                .build();
    }
}
