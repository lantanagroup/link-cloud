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
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;
import com.lantanagroup.link.validation.services.scoring.FindingStatusResolver;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class ScoreAggregatorTest {

    private final ScoreAggregator aggregator = new ScoreAggregator(new FindingStatusResolver());

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

    /**
     * ALL_MUST_PASS is an alias of WORST_OF, so warnings alone no longer fail the result — they
     * surface as ACCEPTABLE_WITH_WARNINGS. It previously collapsed anything short of a clean
     * ACCEPTABLE to UNACCEPTABLE; that was the stricter reading and is the one behaviour change in
     * this rollup. PASS_FAIL remains the strategy that erases the distinction, passing warnings as
     * a plain ACCEPTABLE.
     */
    @Test
    @DisplayName("dimension + ALL_MUST_PASS behaves as WORST_OF: warnings surface, they do not fail")
    void dimensionAllMustPassIsAnAliasOfWorstOf() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(dimResult(PiqiDimension.TERMINOLOGY, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)),
                policy(ScoringPolicyType.PIQI_DIMENSION_SCORECARD, RollupStrategy.ALL_MUST_PASS));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
    }

    @Test
    @DisplayName("ALL_MUST_PASS agrees with WORST_OF for every input, not just the warnings-only case")
    void allMustPassAgreesWithWorstOfAcrossMixedStatuses() {
        List<List<CheckExecutionResult>> cases = List.of(
                List.of(),
                List.of(dimResult(PiqiDimension.CURRENCY, RubricResultStatus.ACCEPTABLE)),
                List.of(dimResult(PiqiDimension.CURRENCY, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)),
                List.of(dimResult(PiqiDimension.CURRENCY, RubricResultStatus.UNACCEPTABLE)),
                List.of(dimResult(PiqiDimension.CURRENCY, RubricResultStatus.ACCEPTABLE_WITH_WARNINGS),
                        dimResult(PiqiDimension.COMPLETENESS, RubricResultStatus.UNACCEPTABLE)));

        for (List<CheckExecutionResult> results : cases) {
            RubricResultStatus worstOf = aggregator.aggregate(results,
                    policy(ScoringPolicyType.PIQI_DIMENSION_SCORECARD, RollupStrategy.WORST_OF)).getInterpretation();
            RubricResultStatus allMustPass = aggregator.aggregate(results,
                    policy(ScoringPolicyType.PIQI_DIMENSION_SCORECARD, RollupStrategy.ALL_MUST_PASS)).getInterpretation();
            assertThat(allMustPass)
                    .as("ALL_MUST_PASS diverged from WORST_OF for %d check(s)", results.size())
                    .isEqualTo(worstOf);
        }
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

    // ------------------------------------------------------------------
    // collapseCheckStatus — post-override findings drive per-check status
    // ------------------------------------------------------------------

    private static EvaluatedFinding uncategorized(Severity severity) {
        return EvaluatedFinding.identity(RawFinding.builder()
                .checkLocalId("c1").dimension(PiqiDimension.CONFORMANCE).severity(severity).build());
    }

    private static EvaluatedFinding categorized(Severity effective, boolean acceptable) {
        RawFinding raw = RawFinding.builder()
                .checkLocalId("c1").dimension(PiqiDimension.CONFORMANCE).severity(Severity.ERROR).build();
        return new EvaluatedFinding(raw, Severity.ERROR, effective, acceptable, List.of("cat-1"), "cat-1");
    }

    @Test
    @DisplayName("collapse takes the worst of multiple findings on one check, whatever their order")
    void collapseTakesTheWorstFinding() {
        List<EvaluatedFinding> findings = List.of(
                uncategorized(Severity.WARNING),
                uncategorized(Severity.ERROR),
                uncategorized(Severity.INFORMATION));

        assertThat(aggregator.collapseCheckStatus(findings)).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("an error a category declared acceptable no longer fails its check")
    void acceptableErrorDoesNotFailTheCheck() {
        assertThat(aggregator.collapseCheckStatus(List.of(categorized(Severity.ERROR, true))))
                .isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
    }

    @Test
    @DisplayName("an unacceptable category fails its check even at INFORMATION severity")
    void unacceptableCategoryFailsTheCheck() {
        assertThat(aggregator.collapseCheckStatus(List.of(categorized(Severity.INFORMATION, false))))
                .isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("null or empty findings collapse to ACCEPTABLE")
    void nullOrEmptyFindingsAreAcceptable() {
        assertThat(aggregator.collapseCheckStatus(null)).isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(aggregator.collapseCheckStatus(List.of())).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    // ------------------------------------------------------------------
    // INCONCLUSIVE ("not evaluated") is ignored by aggregation
    // ------------------------------------------------------------------

    private static EvaluatedFinding notEvaluated() {
        return EvaluatedFinding.identity(RawFinding.builder()
                .checkLocalId("c1").dimension(PiqiDimension.TERMINOLOGY)
                .severity(Severity.INFORMATION).notEvaluated(true).build());
    }

    @Test
    @DisplayName("a check whose findings are all not-evaluated collapses to INCONCLUSIVE")
    void allNotEvaluatedFindingsCollapseToInconclusive() {
        assertThat(aggregator.collapseCheckStatus(List.of(notEvaluated(), notEvaluated())))
                .isEqualTo(RubricResultStatus.INCONCLUSIVE);
    }

    @Test
    @DisplayName("a conclusive failure alongside a not-evaluated finding wins; INCONCLUSIVE is ignored")
    void conclusiveFailureBeatsNotEvaluatedInCollapse() {
        assertThat(aggregator.collapseCheckStatus(List.of(notEvaluated(), uncategorized(Severity.ERROR))))
                .isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("an INCONCLUSIVE dimension is shown in the scorecard but ignored by the overall roll-up")
    void inconclusiveDimensionIsIgnoredInRollup() {
        ScoreCardDto score = dimScore(
                dimResult(PiqiDimension.TERMINOLOGY, RubricResultStatus.INCONCLUSIVE),
                dimResult(PiqiDimension.CONFORMANCE, RubricResultStatus.ACCEPTABLE));

        assertThat(score.getByDimension().get(PiqiDimension.TERMINOLOGY)).isEqualTo(RubricResultStatus.INCONCLUSIVE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("when every scored dimension is INCONCLUSIVE the overall is INCONCLUSIVE")
    void allInconclusiveDimensionsRollUpToInconclusive() {
        ScoreCardDto score = dimScore(
                dimResult(PiqiDimension.TERMINOLOGY, RubricResultStatus.INCONCLUSIVE),
                dimResult(PiqiDimension.CONFORMANCE, RubricResultStatus.INCONCLUSIVE));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.INCONCLUSIVE);
    }

    @Test
    @DisplayName("within a dimension a conclusive status beats INCONCLUSIVE")
    void conclusiveBeatsInconclusiveWithinDimension() {
        ScoreCardDto score = dimScore(
                dimResult(PiqiDimension.PLAUSIBILITY, RubricResultStatus.INCONCLUSIVE),
                dimResult(PiqiDimension.PLAUSIBILITY, RubricResultStatus.UNACCEPTABLE));

        assertThat(score.getByDimension().get(PiqiDimension.PLAUSIBILITY)).isEqualTo(RubricResultStatus.UNACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("check-scorecard roll-up ignores INCONCLUSIVE checks but still lists them")
    void checkScorecardIgnoresInconclusiveInRollup() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(checkResult("c1", RubricResultStatus.INCONCLUSIVE),
                        checkResult("c2", RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)),
                policy(ScoringPolicyType.PIQI_CHECK_SCORECARD, RollupStrategy.WORST_OF));

        assertThat(score.getByCheck()).containsEntry("c1", RubricResultStatus.INCONCLUSIVE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
    }

    @Test
    @DisplayName("pass-fail ignores INCONCLUSIVE checks")
    void passFailIgnoresInconclusive() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(checkResult("c1", RubricResultStatus.INCONCLUSIVE),
                        checkResult("c2", RubricResultStatus.ACCEPTABLE)),
                policy(ScoringPolicyType.PIQI_PASS_FAIL, null));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("pass-fail over only INCONCLUSIVE checks is INCONCLUSIVE")
    void passFailOverOnlyInconclusiveIsInconclusive() {
        ScoreCardDto score = aggregator.aggregate(
                List.of(checkResult("c1", RubricResultStatus.INCONCLUSIVE)),
                policy(ScoringPolicyType.PIQI_PASS_FAIL, null));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.INCONCLUSIVE);
    }
}
