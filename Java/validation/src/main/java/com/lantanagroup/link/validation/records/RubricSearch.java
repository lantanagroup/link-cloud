package com.lantanagroup.link.validation.records;

import com.lantanagroup.link.validation.enums.RubricVersionStatus;

/**
 * Which rubrics the list is asked for. Every part is optional; null or blank means "any".
 *
 * @param status only rubrics with at least one version in this status
 * @param search text found anywhere in the rubric's id, title or owner, whatever its case
 * @param owner  only rubrics with exactly this owner
 */
public record RubricSearch(RubricVersionStatus status, String search, String owner) {

    public static final RubricSearch ANY = new RubricSearch(null, null, null);

    public RubricSearch {
        search = blankToNull(search);
        owner = blankToNull(owner);
    }

    private static String blankToNull(String value) {
        return value == null || value.isBlank() ? null : value.trim();
    }
}
