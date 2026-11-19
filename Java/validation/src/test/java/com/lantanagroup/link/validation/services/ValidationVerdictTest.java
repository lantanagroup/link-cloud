package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class ValidationVerdictTest {

    private static Result result(Category... categories) {
        Result result = new Result();
        result.setCategories(categories.length == 0 ? null : List.of(categories));
        return result;
    }

    private static Category category(boolean acceptable) {
        Category category = new Category();
        category.setAcceptable(acceptable);
        return category;
    }

    @Test
    @DisplayName("legacy passes a patient whose only findings are uncategorized, and says how many it left out")
    void uncategorizedFindingsAreNotCountedButAreReported() {
        List<Result> results = List.of(result(), result());

        assertThat(ValidationVerdict.legacyValid(results)).isTrue();
        assertThat(ValidationVerdict.uncategorizedCount(results)).isEqualTo(2);
    }

    @Test
    @DisplayName("legacy fails on a finding in an unacceptable category; only the uncategorized ones are counted as left out")
    void unacceptableCategoryFails() {
        List<Result> results = List.of(result(category(false)), result(category(true)), result());

        assertThat(ValidationVerdict.legacyValid(results)).isFalse();
        assertThat(ValidationVerdict.uncategorizedCount(results)).isEqualTo(1);
    }
}
