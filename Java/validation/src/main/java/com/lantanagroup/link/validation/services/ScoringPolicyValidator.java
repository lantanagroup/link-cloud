package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.JsonNode;
import com.lantanagroup.link.validation.enums.RollupStrategy;
import com.lantanagroup.link.validation.enums.ScoringPolicyType;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.Set;

/**
 * Validates the declarative {@code scoringPolicy} block of a rubric definition
 * (see Rubric JSON Field Reference §10/§11). The policy is not consumed by any
 * evaluation code yet, so register-time validation is the only gate keeping the
 * stored contract trustworthy: the block is required, allowed keys are exactly
 * {@code type} and {@code rollup}, with values restricted to the documented slugs.
 * {@code rollup} is required for {@code piqi-dimension-scorecard} and
 * {@code piqi-check-scorecard} (both roll a collection of statuses up into one), but is optional
 * for {@code piqi-pass-fail} — {@code ScoreAggregator.aggregatePassFail} never reads it, since
 * WORST_OF/BEST_OF/MAJORITY/etc. are scorecard rollup concepts that don't apply to a single
 * check-based pass/fail verdict.
 */
@Component
public class ScoringPolicyValidator {

    private static final Set<String> ALLOWED_KEYS = Set.of("type", "rollup");

    public void validate(JsonNode scoringPolicy, List<String> errors) {
        if (scoringPolicy == null || scoringPolicy.isNull()) {
            errors.add("scoringPolicy: is required");
            return;
        }
        if (!scoringPolicy.isObject()) {
            errors.add("scoringPolicy: must be a JSON object");
            return;
        }

        scoringPolicy.fieldNames().forEachRemaining(key -> {
            if (!ALLOWED_KEYS.contains(key)) {
                errors.add("scoringPolicy: unknown property '" + key + "'");
            }
        });

        JsonNode type = scoringPolicy.get("type");
        ScoringPolicyType resolvedType = null;
        if (type == null || !type.isTextual() || ScoringPolicyType.fromValue(type.asText()).isEmpty()) {
            errors.add("scoringPolicy.type: is required and must be one of " + ScoringPolicyType.allowedValues());
        } else {
            resolvedType = ScoringPolicyType.fromValue(type.asText()).orElse(null);
        }

        JsonNode rollup = scoringPolicy.get("rollup");
        if (resolvedType == ScoringPolicyType.PIQI_PASS_FAIL) {
            // rollup means nothing for a single pass/fail verdict ΓÇö reject its presence outright
            // rather than silently ignoring it, so a rubric author isn't misled into thinking it
            // has an effect it doesn't.
            if (rollup != null && !rollup.isNull()) {
                errors.add("scoringPolicy.rollup: must not be present for piqi-pass-fail (rollup strategies apply only to scorecard types)");
            }
        } else if (rollup == null) {
            errors.add("scoringPolicy.rollup: is required and must be one of " + RollupStrategy.allowedValues());
        } else if (!rollup.isTextual() || RollupStrategy.fromValue(rollup.asText()).isEmpty()) {
            errors.add("scoringPolicy.rollup: must be one of " + RollupStrategy.allowedValues());
        }
    }
}
