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

    /**
     * The legacy engine passes when every category its findings fall in is acceptable. This is the rule the legacy
     * path has always applied, kept as it is: a finding in no category is not counted, so a patient whose only
     * findings are uncategorized passes, though the categories API treats such findings as unacceptable
     * ({@link Category#UNCATEGORIZED}). Changing it changes which patients pass, so it is not done here; see
     * {@link #uncategorizedCount} for saying how many findings it left out.
     */
    public static boolean legacyValid(List<Result> results) {
        return results.stream()
                .filter(Objects::nonNull)
                .flatMap(result -> result.getCategories() == null ? Stream.empty() : result.getCategories().stream())
                .allMatch(Category::isAcceptable);
    }

    /** How many findings {@link #legacyValid} leaves out because they are in no category. */
    public static long uncategorizedCount(List<Result> results) {
        return results.stream()
                .filter(Objects::nonNull)
                .filter(result -> result.getCategories() == null || result.getCategories().isEmpty())
                .count();
    }

    /** The rubric engine passes when its result status is one of {@link #VALID_RUBRIC_STATUSES}. */
    public static boolean rubricValid(RubricResultStatus status) {
        return status != null && VALID_RUBRIC_STATUSES.contains(status);
    }
}
