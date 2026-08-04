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
 * {@code type} and {@code rollup} (both required), with values restricted to the
 * documented slugs.
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
        if (type == null || !type.isTextual() || ScoringPolicyType.fromValue(type.asText()).isEmpty()) {
            errors.add("scoringPolicy.type: is required and must be one of " + ScoringPolicyType.allowedValues());
        }

        JsonNode rollup = scoringPolicy.get("rollup");
        if (rollup == null || !rollup.isTextual() || RollupStrategy.fromValue(rollup.asText()).isEmpty()) {
            errors.add("scoringPolicy.rollup: is required and must be one of " + RollupStrategy.allowedValues());
        }
    }
}
