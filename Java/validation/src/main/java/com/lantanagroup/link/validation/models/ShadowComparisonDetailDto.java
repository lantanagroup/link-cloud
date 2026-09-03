package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.util.List;

/**
 * Response for the "get shadow comparison by request id" API. Includes the diffs plus both engines'
 * raw results, so you don't have to query the database separately. {@code rubricResult} is null if the
 * legacy engine ran first; {@code legacyResult} is null until the shadow consumer finishes processing.
 */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class ShadowComparisonDetailDto {
    private RubricResultDto rubricResult;
    private LegacyShadowResultDto legacyResult;
    private List<ShadowComparisonResultDto> comparisons;
}
