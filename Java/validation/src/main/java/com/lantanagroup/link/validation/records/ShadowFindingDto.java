package com.lantanagroup.link.validation.records;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import org.hl7.fhir.r4.model.OperationOutcome;

import java.util.List;

/**
 * A lightweight, transport-only snapshot of a {@link Result}, carried on {@link ShadowCompareEvent} instead
 * of the JPA entity itself so the event schema isn't coupled to persistence mapping metadata.
 */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class ShadowFindingDto {
    private OperationOutcome.IssueSeverity severity;
    private OperationOutcome.IssueType code;
    private String message;
    private String location;
    private String expression;
    private List<String> categoryIds;
    private Boolean acceptable;

    public static ShadowFindingDto from(Result result) {
        List<Category> categories = result.getCategories();
        Boolean acceptable = (categories == null || categories.isEmpty())
                ? null
                : categories.stream().allMatch(Category::isAcceptable);
        List<String> categoryIds = (categories == null)
                ? List.of()
                : categories.stream().map(Category::getId).toList();
        return ShadowFindingDto.builder()
                .severity(result.getSeverity())
                .code(result.getCode())
                .message(result.getMessage())
                .location(result.getLocation())
                .expression(result.getExpression())
                .categoryIds(categoryIds)
                .acceptable(acceptable)
                .build();
    }

    /** Reconstructs a transient {@link Result} carrying only the fields {@code ResultDiff} needs to compare. */
    public Result toResult() {
        Result result = new Result();
        result.setSeverity(severity);
        result.setCode(code);
        result.setMessage(message);
        result.setLocation(location);
        result.setExpression(expression);
        return result;
    }
}
