package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.JsonNode;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.Pattern;
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

    @NotBlank
    private String id;

    @NotBlank
    @Pattern(regexp = "^\\d+\\.\\d+\\.\\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$",
            message = "must be a semantic version like 1.2.0")
    private String semver;

    private String title;
    private String owner;
    private List<PiqiDimension> dimensions;
    private JsonNode applicableContext;
    private JsonNode scoringPolicy;

    @Valid
    @NotEmpty
    private List<CheckDto> checks;
}
