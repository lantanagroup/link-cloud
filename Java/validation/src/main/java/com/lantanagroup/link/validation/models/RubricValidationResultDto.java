package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.JsonNode;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import lombok.Builder;
import lombok.Getter;

import java.util.List;

/**
 * What checking a rubric definition found, without saving it.
 */
@Getter
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RubricValidationResultDto {
    // whether registering this definition would be accepted
    private boolean valid;
    // every problem found, each as "<where>: <what>", the same text registering it would return. Empty when valid
    private List<String> errors;
    // the definition as JSON, with its version in canonical form. Present whenever the body could be read,
    // valid or not, so a caller that sent YAML gets JSON back. Absent when the body was not YAML or JSON at all
    private JsonNode definition;
    // the checksum the definition would be stored with. Only when valid
    private String checksum;
    // whether a rubric with this id already exists
    private Boolean rubricExists;
    // the status of the version with this id and semver, when there is one
    private RubricVersionStatus existingStatus;
    // the checksum of that existing version, so a caller can tell an unchanged definition from a changed one
    private String existingChecksum;
}
