package com.lantanagroup.link.validation.services.scoring;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.ValidationPolicyConfig;
import com.lantanagroup.link.validation.enums.RollupStrategy;
import com.lantanagroup.link.validation.enums.ScoringPolicyType;
import com.lantanagroup.link.validation.models.ScoringPolicyDto;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

/**
 * Rollup precedence is rubric version, then configuration, then WORST_OF. A rubric's persisted
 * scoringPolicy.rollup is a deliberate per-rubric decision, so configuration only fills gaps —
 * otherwise setting validation.scoring.rollup would silently restate every existing rubric.
 */
class ScoringPolicyResolverTest {

    private final ObjectMapper objectMapper = new ObjectMapper();
    private final ValidationPolicyConfig config = new ValidationPolicyConfig();
    private final ScoringPolicyResolver resolver = new ScoringPolicyResolver(config, objectMapper);

    @Test
    void theRubricVersionRollupWinsOverConfiguration() {
        config.getScoring().setRollup(RollupStrategy.MAJORITY);

        ScoringPolicyDto resolved = resolver.resolve(
                "{\"type\":\"piqi-check-scorecard\",\"rollup\":\"best-of\"}");

        assertEquals(RollupStrategy.BEST_OF, resolved.getRollup());
        assertEquals(ScoringPolicyType.PIQI_CHECK_SCORECARD, resolved.getType());
    }

    @Test
    void configurationFillsInARollupTheRubricDoesNotName() {
        config.getScoring().setRollup(RollupStrategy.MAJORITY);

        ScoringPolicyDto resolved = resolver.resolve("{\"type\":\"piqi-check-scorecard\"}");

        assertEquals(RollupStrategy.MAJORITY, resolved.getRollup());
    }

    @Test
    void worstOfIsTheFallbackWhenNeitherNamesOne() {
        ScoringPolicyDto resolved = resolver.resolve("{\"type\":\"piqi-check-scorecard\"}");

        assertEquals(RollupStrategy.WORST_OF, resolved.getRollup());
    }

    @Test
    void aMissingPolicyFallsBackToTheDimensionScorecard() {
        ScoringPolicyDto resolved = resolver.resolve((String) null);

        assertEquals(ScoringPolicyType.PIQI_DIMENSION_SCORECARD, resolved.getType());
        assertEquals(RollupStrategy.WORST_OF, resolved.getRollup());
    }

    /** The seeded rubrics declare worst-of explicitly, so configuration must not be able to move them. */
    @Test
    void configurationCannotOverrideAnExplicitRubricRollup() {
        config.getScoring().setRollup(RollupStrategy.PASS_FAIL);

        ScoringPolicyDto resolved = resolver.resolve(
                "{\"type\":\"piqi-dimension-scorecard\",\"rollup\":\"worst-of\"}");

        assertEquals(RollupStrategy.WORST_OF, resolved.getRollup());
    }

    /** A missing rollup now also respects configuration when the whole policy JSON is absent. */
    @Test
    void configurationAppliesWhenThePolicyJsonIsAbsent() {
        config.getScoring().setRollup(RollupStrategy.MAJORITY);

        ScoringPolicyDto resolved = resolver.resolve((String) null);

        assertEquals(RollupStrategy.MAJORITY, resolved.getRollup());
    }
}
