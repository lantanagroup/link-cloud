package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.RubricResultStatus;

import java.util.List;
import java.util.Objects;
import java.util.Set;
import java.util.stream.Stream;

/**
 * Whether a patient's validation passes, under each engine: the decision {@code ValidationComplete.valid} carries.
 * Kept in one place so the shadow comparison judges the engine it runs on the side by the same rule the primary
 * consumer uses.
 */
public final class ValidationVerdict {

    /** Rubric result statuses that count as a pass. */
    static final Set<RubricResultStatus> VALID_RUBRIC_STATUSES = Set.of(
            RubricResultStatus.ACCEPTABLE,
            RubricResultStatus.ACCEPTABLE_WITH_WARNINGS,
            RubricResultStatus.INCONCLUSIVE
    );

    private ValidationVerdict() {
    }

    /** The legacy engine passes when every category its findings fall in is acceptable; uncategorized findings don't count. */
    public static boolean legacyValid(List<Result> results) {
        return results.stream()
                .filter(Objects::nonNull)
                .flatMap(result -> result.getCategories() == null ? Stream.empty() : result.getCategories().stream())
                .allMatch(Category::isAcceptable);
    }

    /** The rubric engine passes when its result status is one of {@link #VALID_RUBRIC_STATUSES}. */
    public static boolean rubricValid(RubricResultStatus status) {
        return status != null && VALID_RUBRIC_STATUSES.contains(status);
    }
}
