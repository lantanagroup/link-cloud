package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
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
public class RubricResultDto {
    private UUID requestId;
    private UUID resultId;
    private String rubricId;
    private UUID rubricVersionId;
    private String correlationId;
    private String requestor;
    private SubjectDto subject;
    private String payloadRef;
    private RubricResultStatus status;
    private ScoreCardDto score;
    private SummaryDto summary;
    private List<FindingDto> findings;
    private TraceDto trace;
}
