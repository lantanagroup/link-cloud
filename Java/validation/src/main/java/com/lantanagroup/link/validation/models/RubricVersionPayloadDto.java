package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.JsonNode;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.util.List;

@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RubricVersionPayloadDto {

    // max sizes mirror the rubric / rubric_version column widths so oversized values fail
    // with a 400 here instead of a SQL truncation error at insert time
    @NotBlank
    @Size(min = 3, max = 128)
    @Pattern(regexp = "^[a-z0-9]+([.-][a-z0-9]+)*$",
            message = "must be lowercase letters/digits in segments separated by '.' or '-' (e.g. piqi.core, bundle-all-dimensions-v1)")
    private String id;

    @NotBlank
    @Size(max = 32, message = "must be at most 32 characters")
    @Pattern(regexp = "^\\d+\\.\\d+\\.\\d+$",
            message = "must be a semantic version like 1.2.0 (MAJOR.MINOR.PATCH, no pre-release tag)")
    private String semver;

    @Size(max = 256)
    private String title;

    @Size(max = 128)
    private String owner;

    @NotEmpty
    private List<PiqiDimension> dimensions;

    private JsonNode applicableContext;

    @NotNull
    private JsonNode scoringPolicy;

    @Valid
    @NotEmpty
    private List<CheckDto> checks;
}
