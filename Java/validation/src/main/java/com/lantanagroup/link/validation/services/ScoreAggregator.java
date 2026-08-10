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
import org.springframework.stereotype.Component;

import java.util.Collection;
import java.util.Comparator;
import java.util.EnumMap;
import java.util.EnumSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.function.Function;
import java.util.stream.Collectors;


@Component
public class ScoreAggregator {

    public ScoreCardDto aggregate(List<RawFinding> findings,
                                  List<CheckExecutionResult> checkResults,
                                  ScoringPolicyDto policy,
                                  Set<PiqiDimension> declaredDimensions) {
        List<RawFinding> safeFindings = findings != null ? findings : List.of();
        List<CheckExecutionResult> safeCheckResults = checkResults != null ? checkResults : List.of();
        ScoringPolicyDto safePolicy = policy != null ? policy : ScoringPolicyDto.defaultPolicy();
        ScoringPolicyType type = safePolicy.getType() != null
                ? safePolicy.getType()
                : ScoringPolicyType.PIQI_DIMENSION_SCORECARD;

        return switch (type) {
            case PIQI_DIMENSION_SCORECARD -> aggregateDimensionScorecard(safeFindings, safePolicy.getRollup(), declaredDimensions);
            case PIQI_CHECK_SCORECARD -> aggregateCheckScorecard(safeCheckResults, safePolicy.getRollup());
            case PIQI_PASS_FAIL -> aggregatePassFail(safeCheckResults);
        };
    }

    // ------------------------------------------------------------------
    // PIQI_DIMENSION_SCORECARD
    // ------------------------------------------------------------------
    private ScoreCardDto aggregateDimensionScorecard(List<RawFinding> findings,
                                                     RollupStrategy rollup,
                                                     Set<PiqiDimension> declaredDimensions) {
        Set<PiqiDimension> applicable = EnumSet.noneOf(PiqiDimension.class);
        if (declaredDimensions != null) {
            applicable.addAll(declaredDimensions);
        }
        for (RawFinding f : findings) {
            if (f.getDimension() != null) {
                applicable.add(f.getDimension());
            }
        }

        Map<PiqiDimension, RubricResultStatus> byDim = new EnumMap<>(PiqiDimension.class);
        for (PiqiDimension d : PiqiDimension.values()) {
            byDim.put(d, applicable.contains(d) ? RubricResultStatus.ACCEPTABLE : RubricResultStatus.NA);
        }
        for (RawFinding f : findings) {
            if (f.getDimension() == null) {
                continue;
            }
            RubricResultStatus current = byDim.get(f.getDimension());
            byDim.put(f.getDimension(), upgrade(current, f.getSeverity()));
        }

        List<RubricResultStatus> scored = applicable.stream().map(byDim::get).collect(Collectors.toList());
        return ScoreCardDto.builder()
                .interpretation(rollUp(scored, rollup))
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

    /** Collapse a single check's findings into one status (used to build per-check results). */
    public RubricResultStatus collapseCheckStatus(List<RawFinding> findings) {
        RubricResultStatus status = RubricResultStatus.ACCEPTABLE;
        if (findings != null) {
            for (RawFinding f : findings) {
                status = upgrade(status, f.getSeverity());
            }
        }
        return status;
    }

    // ------------------------------------------------------------------
    // PIQI_PASS_FAIL
    // ------------------------------------------------------------------
    private ScoreCardDto aggregatePassFail(List<CheckExecutionResult> checkResults) {
        boolean anyFailed = checkResults.stream()
                .anyMatch(r -> binarize(r.getStatus()) == RubricResultStatus.UNACCEPTABLE);
        return ScoreCardDto.builder()
                .interpretation(anyFailed ? RubricResultStatus.UNACCEPTABLE : RubricResultStatus.ACCEPTABLE)
                .build();
    }

    private RubricResultStatus upgrade(RubricResultStatus current, Severity severity) {
        return switch (severity) {
            case ERROR -> RubricResultStatus.UNACCEPTABLE;
            case WARNING -> current == RubricResultStatus.UNACCEPTABLE
                    ? RubricResultStatus.UNACCEPTABLE
                    : RubricResultStatus.ACCEPTABLE_WITH_WARNINGS;
            case INFORMATION -> current;
        };
    }

    private RubricResultStatus rollUp(Collection<RubricResultStatus> statuses, RollupStrategy rollup) {
        RollupStrategy strategy = rollup != null ? rollup : RollupStrategy.WORST_OF;
        return switch (strategy) {
            case WORST_OF -> worstOf(statuses);
            case BEST_OF -> bestOf(statuses);
            case PASS_FAIL -> passFail(statuses);
            case MAJORITY -> majority(statuses);
            case ALL_MUST_PASS -> allMustPass(statuses);
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

    private RubricResultStatus allMustPass(Collection<RubricResultStatus> statuses) {
        return worstOf(statuses) == RubricResultStatus.ACCEPTABLE
                ? RubricResultStatus.ACCEPTABLE
                : RubricResultStatus.UNACCEPTABLE;
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
            case NA -> -1;
            case ACCEPTABLE -> 0;
            case ACCEPTABLE_WITH_WARNINGS -> 1;
            case UNACCEPTABLE -> 2;
            case INCONCLUSIVE -> 3;
        };
    }
}
