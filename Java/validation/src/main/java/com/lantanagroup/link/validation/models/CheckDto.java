package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.JsonNode;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.*;

@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class CheckDto {

    @NotBlank
    private String id;

    @NotNull
    private CheckType type;

    @NotNull
    private PiqiDimension dimension;

    private JsonNode parameters;

    private Severity severityOverride;

    private int ordinal;

    @Builder.Default
    private boolean enabled = true;
}
