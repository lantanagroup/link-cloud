package com.lantanagroup.link.validation.services.categoryoverride;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategorySeverity;
import com.lantanagroup.link.validation.enums.CategoryMatchStrategy;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;

import java.util.List;

public final class CategoryCombiner {

    private CategoryCombiner() {
    }

    public static EvaluatedFinding combine(RawFinding raw, List<Category> matched, CategoryMatchStrategy strategy) {
        if (matched == null || matched.isEmpty()) {
            return EvaluatedFinding.identity(raw);
        }
        return switch (strategy) {
            case FIRST_MATCH -> single(raw, matched.get(0));
            case WORST_OF -> worstOf(raw, matched);
        };
    }

    private static EvaluatedFinding single(RawFinding raw, Category category) {
        return new EvaluatedFinding(
                raw,
                raw.getSeverity(),
                toSeverity(category.getSeverity(), raw.getSeverity()),
                category.isAcceptable(),
                List.of(category.getId()),
                category.getId());
    }

    private static EvaluatedFinding worstOf(RawFinding raw, List<Category> matched) {
        Severity worstSeverity = null;
        boolean anyUnacceptable = false;
        Category governing = null;

        for (Category category : matched) {
            Severity severity = toSeverity(category.getSeverity(), raw.getSeverity());
            if (worstSeverity == null || rank(severity) > rank(worstSeverity)) {
                worstSeverity = severity;
            }
            anyUnacceptable |= !category.isAcceptable();
            if (governing == null || isWorse(category, governing)) {
                governing = category;
            }
        }

        return new EvaluatedFinding(
                raw,
                raw.getSeverity(),
                worstSeverity != null ? worstSeverity : raw.getSeverity(),
                !anyUnacceptable,
                matched.stream().map(Category::getId).toList(),
                governing != null ? governing.getId() : null);
    }

    private static boolean isWorse(Category candidate, Category current) {
        if (candidate.isAcceptable() != current.isAcceptable()) {
            return !candidate.isAcceptable();
        }
        return rank(toSeverity(candidate.getSeverity(), null)) > rank(toSeverity(current.getSeverity(), null));
    }

    private static Severity toSeverity(CategorySeverity categorySeverity, Severity fallback) {
        if (categorySeverity == null) {
            return fallback;
        }
        return switch (categorySeverity) {
            case ERROR -> Severity.ERROR;
            case WARNING -> Severity.WARNING;
            case INFORMATION -> Severity.INFORMATION;
        };
    }

    private static int rank(Severity severity) {
        if (severity == null) {
            return -1;
        }
        return switch (severity) {
            case INFORMATION -> 0;
            case WARNING -> 1;
            case ERROR -> 2;
        };
    }
}
