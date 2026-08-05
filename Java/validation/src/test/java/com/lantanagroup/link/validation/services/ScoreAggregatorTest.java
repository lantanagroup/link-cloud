package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.models.ScoreCardDto;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class ScoreAggregatorTest {

    private final ScoreAggregator aggregator = new ScoreAggregator();

    private static RawFinding finding(PiqiDimension dimension, Severity severity) {
        return RawFinding.builder().dimension(dimension).severity(severity).build();
    }

    @Test
    @DisplayName("no findings -> every dimension ACCEPTABLE and overall ACCEPTABLE")
    void noFindingsIsAcceptable() {
        ScoreCardDto score = aggregator.aggregate(List.of());

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(score.getByDimension()).containsOnlyKeys(PiqiDimension.values());
        assertThat(score.getByDimension().values()).containsOnly(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("an INFORMATION finding does not change the status")
    void informationIsIgnored() {
        ScoreCardDto score = aggregator.aggregate(List.of(finding(PiqiDimension.CONFORMANCE, Severity.INFORMATION)));

        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE)).isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE);
    }

    @Test
    @DisplayName("a WARNING finding downgrades its dimension and the overall to ACCEPTABLE_WITH_WARNINGS")
    void warningDowngradesToWarnings() {
        ScoreCardDto score = aggregator.aggregate(List.of(finding(PiqiDimension.TERMINOLOGY, Severity.WARNING)));

        assertThat(score.getByDimension().get(PiqiDimension.TERMINOLOGY))
                .isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE))
                .isEqualTo(RubricResultStatus.ACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
    }

    @Test
    @DisplayName("an ERROR finding makes its dimension and the overall UNACCEPTABLE")
    void errorIsUnacceptable() {
        ScoreCardDto score = aggregator.aggregate(List.of(finding(PiqiDimension.COMPLETENESS, Severity.ERROR)));

        assertThat(score.getByDimension().get(PiqiDimension.COMPLETENESS))
                .isEqualTo(RubricResultStatus.UNACCEPTABLE);
        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("ERROR outranks WARNING within a dimension regardless of order")
    void errorOutranksWarning() {
        ScoreCardDto score = aggregator.aggregate(List.of(
                finding(PiqiDimension.PLAUSIBILITY, Severity.WARNING),
                finding(PiqiDimension.PLAUSIBILITY, Severity.ERROR)));

        assertThat(score.getByDimension().get(PiqiDimension.PLAUSIBILITY))
                .isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("overall rolls up to the worst dimension: an ERROR anywhere beats WARNINGs elsewhere")
    void overallRollsUpToWorst() {
        ScoreCardDto score = aggregator.aggregate(List.of(
                finding(PiqiDimension.TERMINOLOGY, Severity.WARNING),
                finding(PiqiDimension.CURRENCY, Severity.ERROR)));

        assertThat(score.getInterpretation()).isEqualTo(RubricResultStatus.UNACCEPTABLE);
    }

    @Test
    @DisplayName("a finding with a null dimension is skipped, not crashing the EnumMap-backed aggregation")
    void nullDimensionIsSkipped() {
        ScoreCardDto score = aggregator.aggregate(List.of(
                finding(null, Severity.ERROR),
                finding(PiqiDimension.CONFORMANCE, Severity.WARNING)));

        // The null-dimension finding is ignored; the valid finding still downgrades its dimension.
        assertThat(score.getByDimension().get(PiqiDimension.CONFORMANCE))
                .isEqualTo(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS);
    }
}
