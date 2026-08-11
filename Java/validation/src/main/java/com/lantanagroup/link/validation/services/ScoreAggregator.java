package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RollupStrategy;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.ScoringPolicyType;
import com.lantanagroup.link.validation.models.ScoreCardDto;
import com.lantanagroup.link.validation.models.ScoringPolicyDto;
import com.lantanagroup.link.validation.services.execution.CheckExecutionResult;
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;
import com.lantanagroup.link.validation.services.scoring.FindingStatusResolver;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

import java.util.Collection;
import java.util.Comparator;
import java.util.EnumMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;


@Component
@RequiredArgsConstructor
public class ScoreAggregator {

    private final FindingStatusResolver statusResolver;

    public ScoreCardDto aggregate(List<CheckExecutionResult> checkResults, ScoringPolicyDto policy) {
        List<CheckExecutionResult> safeCheckResults = checkResults != null ? checkResults : List.of();
        ScoringPolicyDto safePolicy = policy != null ? policy : ScoringPolicyDto.defaultPolicy();
        ScoringPolicyType type = safePolicy.getType() != null
                ? safePolicy.getType()
                : ScoringPolicyType.PIQI_DIMENSION_SCORECARD;

        return switch (type) {
            case PIQI_DIMENSION_SCORECARD -> aggregateDimensionScorecard(safeCheckResults, safePolicy.getRollup());
            case PIQI_CHECK_SCORECARD -> aggregateCheckScorecard(safeCheckResults, safePolicy.getRollup());
            case PIQI_PASS_FAIL -> aggregatePassFail(safeCheckResults);
        };
    }

    // ------------------------------------------------------------------
    // PIQI_DIMENSION_SCORECARD
    // ------------------------------------------------------------------
    private ScoreCardDto aggregateDimensionScorecard(List<CheckExecutionResult> checkResults, RollupStrategy rollup) {
        // Seeded only from dimensions that actually have an executed rubric check, not from every
        // PiqiDimension value. A dimension the rubric never checks (or one whose checks were all
        // disabled/skipped) is simply absent from the scorecard rather than reading as a silent
        // ACCEPTABLE. This needs no declared-dimensions metadata from the version — it derives the
        // scored dimensions from the checks that ran.
        Map<PiqiDimension, RubricResultStatus> byDim = new EnumMap<>(PiqiDimension.class);
        for (CheckExecutionResult result : checkResults) {
            PiqiDimension dimension = result.getDimension();
            if (dimension == null) {
                continue;
            }
            byDim.merge(dimension, result.getStatus(), this::worse);
        }
        return ScoreCardDto.builder()
                .interpretation(rollUp(byDim.values(), rollup))
                .byDimension(byDim)
                .build();
    }

    // ------------------------------------------------------------------
    // PIQI_CHECK_SCORECARD
    // ------------------------------------------------------------------
    private ScoreCardDto aggregateCheckScorecard(List<CheckExecutionResult> checkResults, RollupStrategy rollup) {
        Map<String, RubricResultStatus> byCheck = new LinkedHashMap<>();
        for (CheckExecutionResult result : checkResults) {
            byCheck.put(result.getCheckLocalId(), result.getStatus());
        }
        return ScoreCardDto.builder()
                .interpretation(rollUp(byCheck.values(), rollup))
                .byCheck(byCheck)
                .build();
    }

    /**
     * Collapse a single check's findings into one status (used to build per-check results).
     * Findings arrive post category override, so an "acceptable" error no longer fails its check
     * while an uncategorized error still does — see {@link FindingStatusResolver}.
     */
    public RubricResultStatus collapseCheckStatus(List<EvaluatedFinding> findings) {
        RubricResultStatus status = RubricResultStatus.ACCEPTABLE;
        if (findings != null) {
            for (EvaluatedFinding f : findings) {
                status = worse(status, statusResolver.statusOf(f));
            }
        }
        return status;
    }

    // ------------------------------------------------------------------
    // PIQI_PASS_FAIL
    // ------------------------------------------------------------------
    /**
     * Check-based pass/fail: any single failing check fails the whole result; otherwise it passes.
     * scoringPolicy.rollup has no effect here by design — WORST_OF/BEST_OF/MAJORITY/ALL_MUST_PASS
     * are PIQI_CHECK_SCORECARD concepts.
     */
    private ScoreCardDto aggregatePassFail(List<CheckExecutionResult> checkResults) {
        boolean anyFailed = checkResults.stream()
                .anyMatch(r -> binarize(r.getStatus()) == RubricResultStatus.UNACCEPTABLE);
        return ScoreCardDto.builder()
                .interpretation(anyFailed ? RubricResultStatus.UNACCEPTABLE : RubricResultStatus.ACCEPTABLE)
                .build();
    }

    /** Keeps whichever of two statuses is worse (higher severity rank). */
    private RubricResultStatus worse(RubricResultStatus current, RubricResultStatus candidate) {
        return severityRank(candidate) > severityRank(current) ? candidate : current;
    }

    private RubricResultStatus rollUp(Collection<RubricResultStatus> statuses, RollupStrategy rollup) {
        RollupStrategy strategy = rollup != null ? rollup : RollupStrategy.WORST_OF;
        return switch (strategy) {
            // ALL_MUST_PASS is a documented alias of WORST_OF, not a stricter variant: "every entry
            // must pass" and "the overall status is the worst status present" are the same rule once
            // a warning is not treated as a failure. It is retained rather than removed because it
            // is a persisted value in rubric_version.scoring_policy_json.
            case WORST_OF, ALL_MUST_PASS -> worstOf(statuses);
            case BEST_OF -> bestOf(statuses);
            case PASS_FAIL -> passFail(statuses);
            case MAJORITY -> majority(statuses);
        };
    }

    /** Overall status equals the worst status present across all entries. */
    private RubricResultStatus worstOf(Collection<RubricResultStatus> statuses) {
        return statuses.stream()
                .max(Comparator.comparingInt(ScoreAggregator::severityRank))
                .orElse(RubricResultStatus.ACCEPTABLE);
    }

    private RubricResultStatus bestOf(Collection<RubricResultStatus> statuses) {
        return statuses.stream()
                .min(Comparator.comparingInt(ScoreAggregator::severityRank))
                .orElse(RubricResultStatus.ACCEPTABLE);
    }

    private RubricResultStatus passFail(Collection<RubricResultStatus> statuses) {
        return binarize(worstOf(statuses));
    }

    private static RubricResultStatus binarize(RubricResultStatus status) {
        return severityRank(status) >= severityRank(RubricResultStatus.UNACCEPTABLE)
                ? RubricResultStatus.UNACCEPTABLE
                : RubricResultStatus.ACCEPTABLE;
    }

    private RubricResultStatus majority(Collection<RubricResultStatus> statuses) {
        Map<RubricResultStatus, Long> counts = statuses.stream()
                .collect(Collectors.groupingBy(Function.identity(), Collectors.counting()));
        return counts.entrySet().stream()
                .max(Comparator.<Map.Entry<RubricResultStatus, Long>>comparingLong(Map.Entry::getValue)
                        .thenComparingInt(e -> severityRank(e.getKey())))
                .map(Map.Entry::getKey)
                .orElse(RubricResultStatus.ACCEPTABLE);
    }

    private static int severityRank(RubricResultStatus status) {
        return switch (status) {
            case ACCEPTABLE -> 0;
            case ACCEPTABLE_WITH_WARNINGS -> 1;
            case UNACCEPTABLE -> 2;
            case INCONCLUSIVE -> 3;
        };
    }
}
