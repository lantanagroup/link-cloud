package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.enums.Severity;
import jakarta.validation.constraints.NotBlank;
import lombok.*;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;

@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class FacilityOverrideRequestDto {

    @NotBlank
    private String rubricId;

    // optional; when set the override is pinned to this version
    private String rubricVersion;

    private List<String> disabledCheckIds;
    private Map<String, Severity> severityOverrides;
    private Map<String, Object> contextVars;

    private OffsetDateTime effectiveFrom;
    private OffsetDateTime effectiveTo;
}
