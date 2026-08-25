package com.lantanagroup.link.validation.services.scoring;

import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;

/**
 * The precedence rule, exhaustively. The {@code acceptable = null} rows are the important ones:
 * they are the pre-override severity mapping, so a finding no category matched is never downgraded
 * just because the override feature exists.
 */
class FindingStatusResolverTest {

    private final FindingStatusResolver resolver = new FindingStatusResolver();

    private static EvaluatedFinding finding(Severity effective, Boolean acceptable) {
        RawFinding raw = RawFinding.builder()
                .checkLocalId("c1")
                .dimension(PiqiDimension.CONFORMANCE)
                .severity(effective)
                .build();
        if (acceptable == null) {
            return EvaluatedFinding.identity(raw);
        }
        return new EvaluatedFinding(raw, effective, effective, acceptable, List.of("cat-1"), "cat-1");
    }

    @ParameterizedTest(name = "acceptable={0}, severity={1} -> {2}")
    @CsvSource({
            "false, ERROR,       UNACCEPTABLE",
            "false, WARNING,     UNACCEPTABLE",
            "false, INFORMATION, UNACCEPTABLE",
            "true,  ERROR,       ACCEPTABLE_WITH_WARNINGS",
            "true,  WARNING,     ACCEPTABLE_WITH_WARNINGS",
            "true,  INFORMATION, ACCEPTABLE",
    })
    void categorizedFindingsFollowAcceptableFirst(boolean acceptable, Severity severity, RubricResultStatus expected) {
        assertEquals(expected, resolver.statusOf(finding(severity, acceptable)));
    }

    @ParameterizedTest(name = "uncategorized {0} -> {1}")
    @CsvSource({
            "ERROR,       UNACCEPTABLE",
            "WARNING,     ACCEPTABLE_WITH_WARNINGS",
            "INFORMATION, ACCEPTABLE",
    })
    void uncategorizedFindingsKeepThePreOverrideMapping(Severity severity, RubricResultStatus expected) {
        assertEquals(expected, resolver.statusOf(finding(severity, null)));
    }

    /** The regression that matters most: no silent downgrade when acceptable is unknown. */
    @Test
    void anUncategorizedErrorIsNeverDowngraded() {
        assertEquals(RubricResultStatus.UNACCEPTABLE, resolver.statusOf(finding(Severity.ERROR, null)));
    }

    /** The point of the feature: a category may declare an error acceptable, and that must hold. */
    @Test
    void anAcceptableErrorNoLongerFailsTheResult() {
        assertEquals(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS, resolver.statusOf(finding(Severity.ERROR, true)));
    }

    @Test
    void aNullSeverityIsTreatedAsNoSignalRatherThanThrowing() {
        assertEquals(RubricResultStatus.ACCEPTABLE, resolver.statusOf(finding(null, null)));
    }

    /**
     * A check that could not be evaluated (e.g. an unresolvable bound value set) is INCONCLUSIVE, and
     * that wins over any category decision — even a category that declared the finding unacceptable.
     */
    @Test
    void aNotEvaluatedFindingIsInconclusiveRegardlessOfCategory() {
        RawFinding raw = RawFinding.builder()
                .checkLocalId("c1")
                .dimension(PiqiDimension.TERMINOLOGY)
                .severity(Severity.INFORMATION)
                .notEvaluated(true)
                .build();

        assertEquals(RubricResultStatus.INCONCLUSIVE, resolver.statusOf(EvaluatedFinding.identity(raw)));

        EvaluatedFinding categorizedUnacceptable =
                new EvaluatedFinding(raw, Severity.INFORMATION, Severity.ERROR, false, List.of("cat-1"), "cat-1");
        assertEquals(RubricResultStatus.INCONCLUSIVE, resolver.statusOf(categorizedUnacceptable));
    }
}
