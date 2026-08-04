package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import lombok.Builder;
import lombok.Getter;

import java.time.OffsetDateTime;
import java.util.List;

@Getter
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RubricSummaryDto {

    private String rubricId;
    private String title;
    private String owner;
    // All versions of this rubric with their status and per-version metadata.
    private List<RubricVersionSummaryDto> versions;
    private OffsetDateTime createdAt;
    private OffsetDateTime updatedAt;

    public static RubricSummaryDto from(Rubric rubric, List<RubricVersionSummaryDto> versions) {
        return RubricSummaryDto.builder()
                .rubricId(rubric.getRubricId())
                .title(rubric.getTitle())
                .owner(rubric.getOwner())
                .versions(versions)
                .createdAt(rubric.getCreatedAt())
                .updatedAt(rubric.getUpdatedAt())
                .build();
    }

    static List<PiqiDimension> parseDimensions(String json, ObjectMapper objectMapper) {
        if (json == null || json.isBlank()) return null;
        try {
            return objectMapper.readValue(json, new TypeReference<>() {});
        } catch (Exception e) {
            return null;
        }
    }
}
