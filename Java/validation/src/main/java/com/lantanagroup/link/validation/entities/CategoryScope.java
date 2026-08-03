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

    /**
     * Names of HAPI {@code CodedContentValidationAction} enum values to exclude from the action
     * set returned by {@code policyForCodedContent} when the rule matches. Lets a SKIP rule be
     * surgical — remove only the named actions (e.g. {@code ["InvalidDisplay"]}) instead of
     * collapsing the entire action set to empty.
     *
     * <p>When this list is null or empty, the matched rule returns {@code EnumSet.noneOf(...)}
     * (the Phase 1 behaviour: skip every action). When non-empty, the advisor returns
     * {@code EnumSet.complementOf(EnumSet of named actions)} — every action except the named
     * ones still runs. Invalid action names are logged and dropped at load time; if every name
     * fails to resolve, the rule is demoted to LABEL.</p>
     *
     * <p>Combinable with {@link #codeSystems} (and later {@link #valueSets} / {@link #referencePaths}):
     * a rule with both code-system patterns and excludeActions fires only when the system matches
     * and then only removes the named actions.</p>
     */
    private List<String> excludeActions;

    @JsonIgnore
    public boolean isEmpty() {
        return isNullOrEmpty(codeSystems)
                && isNullOrEmpty(valueSets)
                && isNullOrEmpty(referencePaths)
                && isNullOrEmpty(excludeActions);
    }

    private static boolean isNullOrEmpty(List<String> list) {
        return list == null || list.isEmpty();
    }
}
