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

    public static ScoringPolicyDto from(String scoringPolicyJson, ObjectMapper objectMapper) {
        if (scoringPolicyJson == null || scoringPolicyJson.isBlank()) {
            return defaultPolicy();
        }
        try {
            JsonNode node = objectMapper.readTree(scoringPolicyJson);
            ScoringPolicyType type = ScoringPolicyType.fromValue(node.path("type").asText(null))
                    .orElse(ScoringPolicyType.PIQI_DIMENSION_SCORECARD);
            RollupStrategy rollup = RollupStrategy.fromValue(node.path("rollup").asText(null))
                    .orElse(RollupStrategy.WORST_OF);
            return ScoringPolicyDto.builder().type(type).rollup(rollup).build();
        } catch (Exception e) {
            log.warn("Failed to parse scoring policy '{}'; falling back to default: {}",
                    scoringPolicyJson, e.getMessage());
            return defaultPolicy();
        }
    }

    public static ScoringPolicyDto defaultPolicy() {
        return ScoringPolicyDto.builder()
                .type(ScoringPolicyType.PIQI_DIMENSION_SCORECARD)
                .rollup(RollupStrategy.WORST_OF)
                .build();
    }
}
