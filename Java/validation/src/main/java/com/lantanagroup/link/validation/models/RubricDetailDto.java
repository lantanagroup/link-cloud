package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Rubric;
import lombok.Builder;
import lombok.Getter;

import java.time.OffsetDateTime;
import java.util.List;

@Getter
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RubricDetailDto {

    private String rubricId;
    private String title;
    private String owner;
    private String latestPublishedSemver;
    // All versions of this rubric, each carrying its own status and declarative metadata
    // (dimensions, applicableContext, scoringPolicy). Metadata is
    // version-scoped, so it is presented per version rather than as a single rubric-level value.
    private List<RubricVersionSummaryDto> versions;
    private OffsetDateTime createdAt;
    private OffsetDateTime updatedAt;

    public static RubricDetailDto from(Rubric rubric, String latestPublishedSemver,
                                       List<RubricVersionSummaryDto> versions) {
        return RubricDetailDto.builder()
                .rubricId(rubric.getRubricId())
                .title(rubric.getTitle())
                .owner(rubric.getOwner())
                .latestPublishedSemver(latestPublishedSemver)
                .versions(versions)
                .createdAt(rubric.getCreatedAt())
                .updatedAt(rubric.getUpdatedAt())
                .build();
    }

    static JsonNode parseNode(String json, ObjectMapper objectMapper) {
        if (json == null || json.isBlank()) return null;
        try {
            return objectMapper.readTree(json);
        } catch (Exception e) {
            return null;
        }
    }
}
