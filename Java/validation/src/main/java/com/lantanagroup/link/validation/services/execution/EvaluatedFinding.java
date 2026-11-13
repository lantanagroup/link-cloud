package com.lantanagroup.link.validation.services.execution;

import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RawFinding;

import java.util.List;

public record EvaluatedFinding(
        RawFinding raw,
        Severity originalSeverity,
        Severity effectiveSeverity,
        Boolean acceptable,
        List<String> categoryIds,
        String governingCategoryId) {

    /** No category applied: the finding scores on its own severity, exactly as it did pre-override. */
    public static EvaluatedFinding identity(RawFinding raw) {
        return new EvaluatedFinding(raw, raw.getSeverity(), raw.getSeverity(), null, List.of(), null);
    }

    public String checkLocalId() {
        return raw.getCheckLocalId();
    }

    public PiqiDimension dimension() {
        return raw.getDimension();
    }

    /** True when a category actually moved this finding's severity. */
    public boolean severityWasOverridden() {
        return originalSeverity != effectiveSeverity;
    }

    public boolean hasCategories() {
        return categoryIds != null && !categoryIds.isEmpty();
    }

    public boolean notEvaluated() {
        return raw != null && raw.isNotEvaluated();
    }
}
