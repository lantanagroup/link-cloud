package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.util.List;
import java.util.UUID;

@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class FindingDto {
    private UUID id;
    private String checkId;
    private PiqiDimension dimension;

    /** The effective severity: post-override when a category matched, the finding's own otherwise. */
    private Severity severity;

    private String code;
    private String message;
    private String location;
    private String expression;

    /** The severity the check itself emitted, before any category override (diagnostic only). */
    private Severity originalSeverity;

    /** Present only when a category actually moved this finding's severity. */
    private Severity overriddenSeverity;

    /** The governing category's acceptability; null when no category matched. */
    private Boolean acceptable;

    private List<String> categoryIds;

    private String governingCategoryId;
}
