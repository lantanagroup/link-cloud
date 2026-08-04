package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import java.util.ArrayList;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class ScoringPolicyValidatorTest {

    private static final ObjectMapper JSON = new ObjectMapper();

    private final ScoringPolicyValidator validator = new ScoringPolicyValidator();

    private List<String> validate(String json) {
        try {
            JsonNode node = json == null ? null : JSON.readTree(json);
            List<String> errors = new ArrayList<>();
            validator.validate(node, errors);
            return errors;
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    @Test
    @DisplayName("absent scoringPolicy rejected (required)")
    void nullPolicy() {
        assertThat(validate(null)).anyMatch(e -> e.contains("scoringPolicy: is required"));
        assertThat(validate("null")).anyMatch(e -> e.contains("scoringPolicy: is required"));
    }

    @Test
    @DisplayName("non-object policy rejected")
    void nonObject() {
        assertThat(validate("[1,2]")).anyMatch(e -> e.contains("must be a JSON object"));
        assertThat(validate("\"worst-of\"")).anyMatch(e -> e.contains("must be a JSON object"));
    }

    @ParameterizedTest
    @ValueSource(strings = {"piqi-dimension-scorecard", "piqi-check-scorecard", "piqi-pass-fail"})
    @DisplayName("each documented type slug accepted")
    void validTypes(String type) {
        assertThat(validate("{\"type\":\"" + type + "\",\"rollup\":\"worst-of\"}")).isEmpty();
    }

    @ParameterizedTest
    @ValueSource(strings = {"worst-of", "best-of", "pass-fail", "majority", "all-must-pass"})
    @DisplayName("each documented rollup slug accepted")
    void validRollups(String rollup) {
        assertThat(validate("{\"type\":\"piqi-pass-fail\",\"rollup\":\"" + rollup + "\"}")).isEmpty();
    }

    @Test
    @DisplayName("missing or invalid type rejected with allowed values listed")
    void badType() {
        assertThat(validate("{}"))
                .anyMatch(e -> e.contains("scoringPolicy.type: is required") && e.contains("piqi-dimension-scorecard"));
        assertThat(validate("{\"type\":\"weighted\"}"))
                .anyMatch(e -> e.contains("scoringPolicy.type"));
        assertThat(validate("{\"type\":42}"))
                .anyMatch(e -> e.contains("scoringPolicy.type"));
    }

    @Test
    @DisplayName("invalid rollup rejected with allowed values listed")
    void badRollup() {
        assertThat(validate("{\"type\":\"piqi-pass-fail\",\"rollup\":\"average\"}"))
                .anyMatch(e -> e.contains("scoringPolicy.rollup") && e.contains("worst-of"));
    }

    @Test
    @DisplayName("missing rollup rejected (rollup is required when scoringPolicy present)")
    void missingRollup() {
        assertThat(validate("{\"type\":\"piqi-pass-fail\"}"))
                .anyMatch(e -> e.contains("scoringPolicy.rollup: is required"));
    }

    @Test
    @DisplayName("non-textual rollup rejected")
    void nonTextualRollup() {
        assertThat(validate("{\"type\":\"piqi-pass-fail\",\"rollup\":42}"))
                .anyMatch(e -> e.contains("scoringPolicy.rollup"));
    }

    @Test
    @DisplayName("unknown key rejected by name")
    void unknownKey() {
        assertThat(validate("{\"type\":\"piqi-pass-fail\",\"weights\":{}}"))
                .anyMatch(e -> e.contains("unknown property 'weights'"));
    }
}
