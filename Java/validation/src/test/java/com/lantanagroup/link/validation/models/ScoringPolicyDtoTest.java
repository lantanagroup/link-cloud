package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.enums.RollupStrategy;
import com.lantanagroup.link.validation.enums.ScoringPolicyType;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class ScoringPolicyDtoTest {

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void fromParsesTypeAndRollup() {
        ScoringPolicyDto dto = ScoringPolicyDto.from(
                "{\"type\":\"piqi-dimension-scorecard\",\"rollup\":\"worst-of\"}", objectMapper);

        assertThat(dto.getType()).isEqualTo(ScoringPolicyType.PIQI_DIMENSION_SCORECARD);
        assertThat(dto.getRollup()).isEqualTo(RollupStrategy.WORST_OF);
    }

    @Test
    void fromNullOrBlankJsonReturnsAnEmptyDto() {
        assertThat(ScoringPolicyDto.from(null, objectMapper).getType()).isNull();
        assertThat(ScoringPolicyDto.from("", objectMapper).getType()).isNull();
        assertThat(ScoringPolicyDto.from("   ", objectMapper).getType()).isNull();
    }

    @Test
    void fromMalformedJsonFallsBackToAnEmptyDtoInsteadOfThrowing() {
        ScoringPolicyDto dto = ScoringPolicyDto.from("{ not valid json", objectMapper);

        assertThat(dto.getType()).isNull();
        assertThat(dto.getRollup()).isNull();
    }

    @Test
    void fromAnUnknownTypeOrRollupLeavesTheFieldNullRatherThanThrowing() {
        ScoringPolicyDto dto = ScoringPolicyDto.from(
                "{\"type\":\"not-a-real-type\",\"rollup\":\"not-a-real-rollup\"}", objectMapper);

        assertThat(dto.getType()).isNull();
        assertThat(dto.getRollup()).isNull();
    }

    @Test
    void fromPreservesAbsentFieldsAsNullRatherThanDefaulting() {
        // Documented precedence: null here must fall through to config, then WORST_OF -- defaulting at
        // parse time would silently outrank the configuration fallback.
        ScoringPolicyDto dto = ScoringPolicyDto.from("{\"type\":\"piqi-dimension-scorecard\"}", objectMapper);

        assertThat(dto.getType()).isEqualTo(ScoringPolicyType.PIQI_DIMENSION_SCORECARD);
        assertThat(dto.getRollup()).isNull();
    }

    @Test
    void defaultPolicyIsPiqiDimensionScorecardWithWorstOfRollup() {
        ScoringPolicyDto dto = ScoringPolicyDto.defaultPolicy();

        assertThat(dto.getType()).isEqualTo(ScoringPolicyType.PIQI_DIMENSION_SCORECARD);
        assertThat(dto.getRollup()).isEqualTo(RollupStrategy.WORST_OF);
    }
}
