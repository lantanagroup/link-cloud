package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import jakarta.validation.constraints.Pattern;
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
public class SubjectDto {

    private static final String ALLOWED_PATTERN = "^[A-Za-z0-9-]*$";
    private static final String ALLOWED_MESSAGE = "must contain only letters, numbers, and '-'";

    @Pattern(regexp = ALLOWED_PATTERN, message = ALLOWED_MESSAGE)
    private String facilityId;

    @Pattern(regexp = ALLOWED_PATTERN, message = ALLOWED_MESSAGE)
    private String patientId;

    @Pattern(regexp = ALLOWED_PATTERN, message = ALLOWED_MESSAGE)
    private String reportId;

    @Pattern(regexp = ALLOWED_PATTERN, message = ALLOWED_MESSAGE)
    private String workflow;

    @Pattern(regexp = ALLOWED_PATTERN, message = ALLOWED_MESSAGE)
    private String stage;
}
