package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.enums.RollupStrategy;
import com.lantanagroup.link.validation.enums.ScoringPolicyType;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import lombok.extern.slf4j.Slf4j;


@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@Slf4j
public class ScoringPolicyDto {

    private ScoringPolicyType type;
    private RollupStrategy rollup;

    /**
     * Parses a persisted scoring policy, preserving absent fields as null so that
     * {@code ScoringPolicyResolver} can apply its precedence (rubric, then configuration, then
     * WORST_OF). Defaulting at parse time would silently outrank the configuration fallback.
     * {@code ScoreAggregator}-side null handling keeps direct callers safe.
     */
    public static ScoringPolicyDto from(String scoringPolicyJson, ObjectMapper objectMapper) {
        if (scoringPolicyJson == null || scoringPolicyJson.isBlank()) {
            return ScoringPolicyDto.builder().build();
        }
        try {
            JsonNode node = objectMapper.readTree(scoringPolicyJson);
            ScoringPolicyType type = ScoringPolicyType.fromValue(node.path("type").asText(null))
                    .orElse(null);
            RollupStrategy rollup = RollupStrategy.fromValue(node.path("rollup").asText(null))
                    .orElse(null);
            return ScoringPolicyDto.builder().type(type).rollup(rollup).build();
        } catch (Exception e) {
            log.warn("Failed to parse scoring policy '{}'; falling back to default: {}",
                    scoringPolicyJson, e.getMessage());
            return ScoringPolicyDto.builder().build();
        }
    }

    public static ScoringPolicyDto defaultPolicy() {
        return ScoringPolicyDto.builder()
                .type(ScoringPolicyType.PIQI_DIMENSION_SCORECARD)
                .rollup(RollupStrategy.WORST_OF)
                .build();
    }
}
