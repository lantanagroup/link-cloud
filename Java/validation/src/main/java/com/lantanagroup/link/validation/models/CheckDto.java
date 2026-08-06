package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.JsonNode;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.PositiveOrZero;
import jakarta.validation.constraints.Size;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class CheckDto {

    // mirrors rubric_check.check_local_id varchar(64)
    @NotBlank
    @Size(max = 64)
    private String id;

    @NotNull
    private CheckType type;

    @NotNull
    private PiqiDimension dimension;

    private JsonNode parameters;

    @NotNull
    private Severity severityOverride;

    // optional. explicit ordinals must be unique per rubric (see RubricDefinitionValidator)
    @PositiveOrZero
    private Integer ordinal;

    @Builder.Default
    private boolean enabled = true;
}
