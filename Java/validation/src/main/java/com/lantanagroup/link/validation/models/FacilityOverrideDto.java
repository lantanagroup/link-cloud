package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.FacilityOverride;
import com.lantanagroup.link.validation.enums.Severity;
import lombok.Builder;
import lombok.Getter;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;

@Getter
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class FacilityOverrideDto {

    private UUID overrideId;
    private String facilityId;
    private String rubricId;
    private UUID rubricVersionId;
    private String rubricVersion;
    private List<String> disabledCheckIds;
    private Map<String, Severity> severityOverrides;
    private Map<String, Object> contextVars;
    private OffsetDateTime effectiveFrom;
    private OffsetDateTime effectiveTo;
    private OffsetDateTime createdAt;
    private String createdBy;

    public static FacilityOverrideDto from(FacilityOverride override, String rubricVersion,
                                           ObjectMapper objectMapper) {
        return FacilityOverrideDto.builder()
                .overrideId(override.getOverrideId())
                .facilityId(override.getFacilityId())
                .rubricId(override.getRubricId())
                .rubricVersionId(override.getRubricVersionId())
                .rubricVersion(rubricVersion)
                .disabledCheckIds(parse(override.getDisabledCheckIdsJson(), objectMapper, new TypeReference<>() {}))
                .severityOverrides(parse(override.getSeverityOverridesJson(), objectMapper, new TypeReference<>() {}))
                .contextVars(parse(override.getContextVarsJson(), objectMapper, new TypeReference<>() {}))
                .effectiveFrom(override.getEffectiveFrom())
                .effectiveTo(override.getEffectiveTo())
                .createdAt(override.getCreatedAt())
                .createdBy(override.getCreatedBy())
                .build();
    }

    private static <T> T parse(String json, ObjectMapper objectMapper, TypeReference<T> type) {
        if (json == null || json.isBlank()) return null;
        try {
            return objectMapper.readValue(json, type);
        } catch (Exception e) {
            return null;
        }
    }
}
