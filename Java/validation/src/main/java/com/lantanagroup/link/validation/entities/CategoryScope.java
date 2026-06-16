package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.Getter;
import lombok.Setter;

import java.util.List;

/**
 * Scoping metadata for {@link CategoryStrategy#SKIP} rules: tells the
 * {@code CategoryBackedPolicyAdvisor} which HAPI validation decision-point inputs the
 * rule should fire against. Each list is a set of regex patterns; a match against any one
 * pattern is sufficient for the scope to apply.
 *
 * <p>Only {@link #codeSystems} is consumed in Phase 1 (via {@code policyForCodedContent}).
 * The {@link #valueSets} and {@link #referencePaths} fields are present in the schema for
 * forward compatibility with later phases — {@code valueSets} pairs with
 * {@code policyForCodedContent}'s bound ValueSet URL, {@code referencePaths} pairs with
 * {@code policyForReference}'s path/type inputs.</p>
 *
 * <p>{@code null} or empty lists are treated as "no scope on this axis" — the rule simply
 * doesn't constrain that axis. A scope with all axes null or empty is meaningless for
 * {@code SKIP} and will be rejected at category-load time.</p>
 */
@Getter
@Setter
@JsonInclude(JsonInclude.Include.NON_NULL)
public class CategoryScope {
    /**
     * Regex patterns matched against each entry of {@code policyForCodedContent}'s
     * {@code List<String> systems} argument. Used by Phase 1.
     */
    private List<String> codeSystems;

    /**
     * Regex patterns matched against the bound {@code ValueSet.url} of the
     * {@code policyForCodedContent} call. Reserved for Phase 1 follow-up migrations.
     */
    private List<String> valueSets;

    /**
     * Regex patterns matched against {@code policyForReference}'s path / target inputs.
     * Reserved for Phase 3.
     */
    private List<String> referencePaths;

    @JsonIgnore
    public boolean isEmpty() {
        return isNullOrEmpty(codeSystems)
                && isNullOrEmpty(valueSets)
                && isNullOrEmpty(referencePaths);
    }

    private static boolean isNullOrEmpty(List<String> list) {
        return list == null || list.isEmpty();
    }
}
