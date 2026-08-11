package com.lantanagroup.link.validation.services.scoring;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.ValidationPolicyConfig;
import com.lantanagroup.link.validation.enums.RollupStrategy;
import com.lantanagroup.link.validation.enums.ScoringPolicyType;
import com.lantanagroup.link.validation.models.ScoringPolicyDto;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

@Component
@RequiredArgsConstructor
public class ScoringPolicyResolver {

    private final ValidationPolicyConfig config;
    private final ObjectMapper objectMapper;

    public ScoringPolicyDto resolve(String scoringPolicyJson) {
        return resolve(ScoringPolicyDto.from(scoringPolicyJson, objectMapper));
    }

    public ScoringPolicyDto resolve(ScoringPolicyDto policy) {
        ScoringPolicyDto safe = policy != null ? policy : ScoringPolicyDto.defaultPolicy();
        return ScoringPolicyDto.builder()
                .type(safe.getType() != null ? safe.getType() : ScoringPolicyType.PIQI_DIMENSION_SCORECARD)
                .rollup(resolveRollup(safe.getRollup()))
                .build();
    }

    private RollupStrategy resolveRollup(RollupStrategy fromRubric) {
        if (fromRubric != null) {
            return fromRubric;
        }
        RollupStrategy fromConfig = config.getScoring().getRollup();
        return fromConfig != null ? fromConfig : RollupStrategy.WORST_OF;
    }
}
