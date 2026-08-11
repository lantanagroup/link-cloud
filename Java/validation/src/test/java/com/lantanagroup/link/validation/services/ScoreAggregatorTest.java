package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RollupStrategy;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.ScoringPolicyType;
import com.lantanagroup.link.validation.models.ScoreCardDto;
import com.lantanagroup.link.validation.models.ScoringPolicyDto;
import com.lantanagroup.link.validation.services.execution.CheckExecutionResult;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class ScoreAggregatorTest {

    private final ScoreAggregator aggregator = new ScoreAggregator();

    /** A per-check result carrying a dimension (what the dimension scorecard scores from). */
    private static CheckExecutionResult dimResult(PiqiDimension dimension, RubricResultStatus status) {
        return CheckExecutionResult.builder()
                .checkLocalId("c-" + dimension + "-" + status)
                .dimension(dimension)
                .status(status)
                .build();
    }

    private static CheckExecutionResult checkResult(String id, RubricResultStatus status) {
        return CheckExecutionResult.builder().checkLocalId(id).status(status).build();
    }

    private static ScoringPolicyDto policy(ScoringPolicyType type, RollupStrategy rollup) {
        return ScoringPolicyDto.builder().type(type).rollup(rollup).build();
    }

    // default policy = PIQI_DIMENSION_SCORECARD + WORST_OF
    private ScoreCardDto dimScore(CheckExecutionResult... results) {
        return aggregator.aggregate(List.of(results), ScoringPolicyDto.defaultPolicy());
    }

    // ------------------------------------------------------------------
    // PIQI_DIMENSION_SCORECARD + WORST_OF (the default)
    // ------------------------------------------------------------------

    @Test
    @DisplayName("no checks -> empty scorecard and overall ACCEPTABLE")
    void noChecksIsAcceptable() {
        ScoreCardDto score = dimScore();

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(score.getByDimension()).isEmpty();
    }

    @Test
    @DisplayName("a clean check contributes its dimension as ACCEPTABLE")
    void cleanCheckScoresDimensionAcceptable() {
        ScoreCardDto score = dimScore(dimResult(PiqiDimension.CONFORMANCE, RubricResultStatus.ACCEPTABLE));

        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE)).isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("a WARNING check downgrades its dimension and the overall to ACCEPTABLE_WITH_WARNINGS")
    void warningDowngradesToWarnings() {
        ScoreCardDto score = dimScore(dimResult(PiqiDimension.TERMINOLOGY, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS));

        assertThat(score.getByDimension().get(PiqiDimension.TERMINOLOGY))
                .isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
    }

    @Test
    @DisplayName("an ERROR check makes its dimension and the overall UNACCEPTABLE")
    void errorIsUnacceptable() {
        ScoreCardDto score = dimScore(dimResult(PiqiDimension.COMPLETENESS, RubricResultStatus.UNACCEPTABLE));

        assertThat(score.getByDimension().get(PiqiDimension.COMPLETENESS))
                .isEqualTo(RubricResultStatus.UNACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("two checks in one dimension collapse to the worse status, regardless of order")
    void worseStatusWinsWithinDimension() {
        ScoreCardDto score = dimScore(
                dimResult(PiqiDimension.PLAUSIBILITY, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS),
                dimResult(PiqiDimension.PLAUSIBILITY, RubricResultStatus.UNACCEPTABLE));

        assertThat(score.getByDimension().get(PiqiDimension.PLAUSIBILITY))
                .isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("overall rolls up to the worst dimension: an ERROR anywhere beats WARNINGs elsewhere")
    void overallRollsUpToWorst() {
        ScoreCardDto score = dimScore(
                dimResult(PiqiDimension.TERMINOLOGY, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS),
                dimResult(PiqiDimension.CURRENCY, RubricResultStatus.UNACCEPTABLE));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("a check with a null dimension is skipped, not crashing the EnumMap-backed aggregation")
    void nullDimensionIsSkipped() {
        ScoreCardDto score = dimScore(
                checkResult("no-dim", RubricResultStatus.UNACCEPTABLE),
                dimResult(PiqiDimension.CONFORMANCE, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS));

        assertThat(score.getByDimension()).containsOnlyKeys(PiqiDimension.CONFORMANCE);
        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE))
                .isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
    }

    // ------------------------------------------------------------------
    // PIQI_DIMENSION_SCORECARD — dimensions are scoped to what actually ran
    // ------------------------------------------------------------------

    @Test
    @DisplayName("only checked dimensions appear: a CONFORMANCE-only rubric scores CONFORMANCE and omits the rest (no phantom ACCEPTABLE, no NA)")
    void onlyCheckedDimensionsAppear() {
        ScoreCardDto score = dimScore(dimResult(PiqiDimension.CONFORMANCE, RubricResultStatus.UNACCEPTABLE));

        assertThat(score.getByDimension()).containsOnlyKeys(PiqiDimension.CONFORMANCE);
        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE)).isEqualTo(RubricResultStatus.UNACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("regression: BEST_OF over a single scored dimension does not pick a phantom ACCEPTABLE from an unchecked one")
    void bestOfDoesNotInventCleanDimensions() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(dimResult(PiqiDimension.CONFORMANCE, RubricResultStatus.UNACCEPTABLE)),
                policy(ScoringPolicyType.PIQI_DIMENSION_SCORECARD, RollupStrategy.BEST_OF));

        assertThat(score.getByDimension()).containsOnlyKeys(PiqiDimension.CONFORMANCE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    // ------------------------------------------------------------------
    // PIQI_DIMENSION_SCORECARD — non-default rollups
    // ------------------------------------------------------------------

    @Test
    @DisplayName("dimension + BEST_OF passes overall when at least one scored dimension is clean, even with an ERROR elsewhere")
    void dimensionBestOfPassesWhenAnyDimensionClean() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(dimResult(PiqiDimension.CONFORMANCE, RubricResultStatus.UNACCEPTABLE),
                        dimResult(PiqiDimension.TERMINOLOGY, RubricResultStatus.ACCEPTABLE)),
                policy(ScoringPolicyType.PIQI_DIMENSION_SCORECARD, RollupStrategy.BEST_OF));

        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE)).isEqualTo(RubricResultStatus.UNACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("dimension + ALL_MUST_PASS fails overall on a mere WARNING (stricter than a clean pass)")
    void dimensionAllMustPassFailsOnWarning() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(dimResult(PiqiDimension.TERMINOLOGY, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)),
                policy(ScoringPolicyType.PIQI_DIMENSION_SCORECARD, RollupStrategy.ALL_MUST_PASS));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    // ------------------------------------------------------------------
    // PIQI_CHECK_SCORECARD
    // ------------------------------------------------------------------

    @Test
    @DisplayName("check-scorecard + WORST_OF exposes per-check statuses and rolls up to the worst check")
    void checkScorecardWorstOf() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(checkResult("c1", RubricResultStatus.ACCEPTABLE),
                        checkResult("c2", RubricResultStatus.ACCEPTABLE_WITH_WARNINGS),
                        checkResult("c3", RubricResultStatus.UNACCEPTABLE)),
                policy(ScoringPolicyType.PIQI_CHECK_SCORECARD, RollupStrategy.WORST_OF));

        assertThat(score.getByCheck()).hasSize(3)
                .containsEntry("c3", RubricResultStatus.UNACCEPTABLE);
        assertThat(score.getByDimension()).isNull();
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("check-scorecard + BEST_OF rolls up to the cleanest check")
    void checkScorecardBestOf() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(checkResult("c1", RubricResultStatus.ACCEPTABLE),
                        checkResult("c2", RubricResultStatus.UNACCEPTABLE)),
                policy(ScoringPolicyType.PIQI_CHECK_SCORECARD, RollupStrategy.BEST_OF));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("check-scorecard + MAJORITY takes the most common status")
    void checkScorecardMajority() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(checkResult("c1", RubricResultStatus.ACCEPTABLE),
                        checkResult("c2", RubricResultStatus.ACCEPTABLE),
                        checkResult("c3", RubricResultStatus.UNACCEPTABLE)),
                policy(ScoringPolicyType.PIQI_CHECK_SCORECARD, RollupStrategy.MAJORITY));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    // ------------------------------------------------------------------
    // PIQI_PASS_FAIL
    // ------------------------------------------------------------------

    @Test
    @DisplayName("pass-fail fails when any single check fails, regardless of rollup")
    void passFailFailsOnAnyFailingCheck() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(checkResult("c1", RubricResultStatus.ACCEPTABLE),
                        checkResult("c2", RubricResultStatus.UNACCEPTABLE)),
                policy(ScoringPolicyType.PIQI_PASS_FAIL, RollupStrategy.WORST_OF));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
        assertThat(score.getByDimension()).isNull();
        assertThat(score.getByCheck()).isNull();
    }

    @Test
    @DisplayName("pass-fail passes when every check is acceptable (warnings still pass)")
    void passFailPassesWhenNoFailingCheck() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(checkResult("c1", RubricResultStatus.ACCEPTABLE),
                        checkResult("c2", RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)),
                policy(ScoringPolicyType.PIQI_PASS_FAIL, null));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }
}
