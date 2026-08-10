package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RollupStrategy;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.ScoringPolicyType;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.models.ScoreCardDto;
import com.lantanagroup.link.validation.models.ScoringPolicyDto;
import com.lantanagroup.link.validation.services.execution.CheckExecutionResult;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class ScoreAggregatorTest {

    private final ScoreAggregator aggregator = new ScoreAggregator();

    private static RawFinding finding(PiqiDimension dimension, Severity severity) {
        return RawFinding.builder().dimension(dimension).severity(severity).build();
    }

    private static CheckExecutionResult checkResult(String id, RubricResultStatus status) {
        return CheckExecutionResult.builder().checkLocalId(id).status(status).build();
    }

    private static ScoringPolicyDto policy(ScoringPolicyType type, RollupStrategy rollup) {
        return ScoringPolicyDto.builder().type(type).rollup(rollup).build();
    }

    // default policy = PIQI_DIMENSION_SCORECARD + WORST_OF, matching the pre-enhancement behaviour
    private ScoreCardDto aggregate(List<RawFinding> findings) {
        return aggregator.aggregate(findings, List.of(), ScoringPolicyDto.defaultPolicy());
    }

    // ------------------------------------------------------------------
    // PIQI_DIMENSION_SCORECARD + WORST_OF (the default) — original behaviour
    // ------------------------------------------------------------------

    @Test
    @DisplayName("no findings -> every dimension ACCEPTABLE and overall ACCEPTABLE")
    void noFindingsIsAcceptable() {
        ScoreCardDto score = aggregate(List.of());

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(score.getByDimension()).containsOnlyKeys(PiqiDimension.values());
        assertThat(score.getByDimension().values()).containsOnly(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("an INFORMATION finding does not change the status")
    void informationIsIgnored() {
        ScoreCardDto score = aggregate(List.of(finding(PiqiDimension.CONFORMANCE, Severity.INFORMATION)));

        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE)).isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("a WARNING finding downgrades its dimension and the overall to ACCEPTABLE_WITH_WARNINGS")
    void warningDowngradesToWarnings() {
        ScoreCardDto score = aggregate(List.of(finding(PiqiDimension.TERMINOLOGY, Severity.WARNING)));

        assertThat(score.getByDimension().get(PiqiDimension.TERMINOLOGY))
                .isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE))
                .isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
    }

    @Test
    @DisplayName("an ERROR finding makes its dimension and the overall UNACCEPTABLE")
    void errorIsUnacceptable() {
        ScoreCardDto score = aggregate(List.of(finding(PiqiDimension.COMPLETENESS, Severity.ERROR)));

        assertThat(score.getByDimension().get(PiqiDimension.COMPLETENESS))
                .isEqualTo(RubricResultStatus.UNACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("ERROR outranks WARNING within a dimension regardless of order")
    void errorOutranksWarning() {
        ScoreCardDto score = aggregate(List.of(
                finding(PiqiDimension.PLAUSIBILITY, Severity.WARNING),
                finding(PiqiDimension.PLAUSIBILITY, Severity.ERROR)));

        assertThat(score.getByDimension().get(PiqiDimension.PLAUSIBILITY))
                .isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("overall rolls up to the worst dimension: an ERROR anywhere beats WARNINGs elsewhere")
    void overallRollsUpToWorst() {
        ScoreCardDto score = aggregate(List.of(
                finding(PiqiDimension.TERMINOLOGY, Severity.WARNING),
                finding(PiqiDimension.CURRENCY, Severity.ERROR)));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("a finding with a null dimension is skipped, not crashing the EnumMap-backed aggregation")
    void nullDimensionIsSkipped() {
        ScoreCardDto score = aggregate(List.of(
                finding(null, Severity.ERROR),
                finding(PiqiDimension.CONFORMANCE, Severity.WARNING)));

        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE))
                .isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
    }

    // ------------------------------------------------------------------
    // PIQI_DIMENSION_SCORECARD — non-default rollups
    // ------------------------------------------------------------------

    @Test
    @DisplayName("dimension + BEST_OF passes overall when at least one dimension is clean, even with an ERROR elsewhere")
    void dimensionBestOfPassesWhenAnyDimensionClean() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(finding(PiqiDimension.CONFORMANCE, Severity.ERROR)),
                List.of(),
                policy(ScoringPolicyType.PIQI_DIMENSION_SCORECARD, RollupStrategy.BEST_OF));

        // the failing dimension is still recorded...
        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE)).isEqualTo(RubricResultStatus.UNACCEPTABLE);
        // ...but best-of rolls up to the cleanest dimension
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("dimension + ALL_MUST_PASS fails overall on a mere WARNING (stricter than worst-of)")
    void dimensionAllMustPassFailsOnWarning() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(finding(PiqiDimension.TERMINOLOGY, Severity.WARNING)),
                List.of(),
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
                List.of(),
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
                List.of(),
                List.of(checkResult("c1", RubricResultStatus.ACCEPTABLE),
                        checkResult("c2", RubricResultStatus.UNACCEPTABLE)),
                policy(ScoringPolicyType.PIQI_CHECK_SCORECARD, RollupStrategy.BEST_OF));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("check-scorecard + MAJORITY takes the most common status")
    void checkScorecardMajority() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(),
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
                List.of(),
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
                List.of(),
                List.of(checkResult("c1", RubricResultStatus.ACCEPTABLE),
                        checkResult("c2", RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)),
                policy(ScoringPolicyType.PIQI_PASS_FAIL, null));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }
}
