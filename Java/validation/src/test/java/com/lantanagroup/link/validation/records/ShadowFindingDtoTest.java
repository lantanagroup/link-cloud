package com.lantanagroup.link.validation.records;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategorySeverity;
import com.lantanagroup.link.validation.entities.Result;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class ShadowFindingDtoTest {

    private static Category category(String id, boolean acceptable) {
        Category category = new Category();
        category.setId(id);
        category.setTitle(id);
        category.setSeverity(CategorySeverity.WARNING);
        category.setAcceptable(acceptable);
        category.setGuidance("guidance");
        return category;
    }

    private static Result result(List<Category> categories) {
        Result result = new Result();
        result.setSeverity(OperationOutcome.IssueSeverity.WARNING);
        result.setCode(OperationOutcome.IssueType.INVALID);
        result.setMessage("some message");
        result.setLocation("Patient.name[0]");
        result.setExpression("Patient.name[0]");
        result.setCategories(categories);
        return result;
    }

    @Test
    void fromWithNoCategoriesLeavesAcceptableNullAndCategoryIdsEmpty() {
        ShadowFindingDto dto = ShadowFindingDto.from(result(null));

        assertThat(dto.getAcceptable()).isNull();
        assertThat(dto.getCategoryIds()).isEmpty();
    }

    @Test
    void fromWithEmptyCategoryListLeavesAcceptableNull() {
        ShadowFindingDto dto = ShadowFindingDto.from(result(List.of()));

        assertThat(dto.getAcceptable()).isNull();
        assertThat(dto.getCategoryIds()).isEmpty();
    }

    @Test
    void fromIsAcceptableOnlyWhenEveryMatchedCategoryIsAcceptable() {
        ShadowFindingDto allAcceptable = ShadowFindingDto.from(
                result(List.of(category("a", true), category("b", true))));
        ShadowFindingDto oneUnacceptable = ShadowFindingDto.from(
                result(List.of(category("a", true), category("b", false))));

        assertThat(allAcceptable.getAcceptable()).isTrue();
        assertThat(oneUnacceptable.getAcceptable()).isFalse();
        assertThat(oneUnacceptable.getCategoryIds()).containsExactly("a", "b");
    }

    @Test
    void fromCopiesTheCoreFieldsFromTheResult() {
        ShadowFindingDto dto = ShadowFindingDto.from(result(null));

        assertThat(dto.getSeverity()).isEqualTo(OperationOutcome.IssueSeverity.WARNING);
        assertThat(dto.getCode()).isEqualTo(OperationOutcome.IssueType.INVALID);
        assertThat(dto.getMessage()).isEqualTo("some message");
        assertThat(dto.getLocation()).isEqualTo("Patient.name[0]");
        assertThat(dto.getExpression()).isEqualTo("Patient.name[0]");
    }

    @Test
    void toResultReconstructsOnlyTheComparisonFields() {
        ShadowFindingDto dto = ShadowFindingDto.builder()
                .severity(OperationOutcome.IssueSeverity.ERROR)
                .code(OperationOutcome.IssueType.CODEINVALID)
                .message("m")
                .location("loc")
                .expression("expr")
                .categoryIds(List.of("a"))
                .acceptable(true)
                .build();

        Result result = dto.toResult();

        assertThat(result.getSeverity()).isEqualTo(OperationOutcome.IssueSeverity.ERROR);
        assertThat(result.getCode()).isEqualTo(OperationOutcome.IssueType.CODEINVALID);
        assertThat(result.getMessage()).isEqualTo("m");
        assertThat(result.getLocation()).isEqualTo("loc");
        assertThat(result.getExpression()).isEqualTo("expr");
    }
}
