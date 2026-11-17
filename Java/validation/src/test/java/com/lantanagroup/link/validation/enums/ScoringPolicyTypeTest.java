package com.lantanagroup.link.validation.enums;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class ScoringPolicyTypeTest {

    @Test
    @DisplayName("resolves each documented slug")
    void fromValueResolvesSlugs() {
        assertThat(ScoringPolicyType.fromValue("piqi-dimension-scorecard"))
                .contains(ScoringPolicyType.PIQI_DIMENSION_SCORECARD);
        assertThat(ScoringPolicyType.fromValue("piqi-check-scorecard"))
                .contains(ScoringPolicyType.PIQI_CHECK_SCORECARD);
        assertThat(ScoringPolicyType.fromValue("piqi-pass-fail"))
                .contains(ScoringPolicyType.PIQI_PASS_FAIL);
    }

    @Test
    @DisplayName("unknown, wrong-case, and null-ish slugs resolve empty")
    void fromValueRejectsUnknown() {
        assertThat(ScoringPolicyType.fromValue("weighted")).isEmpty();
        assertThat(ScoringPolicyType.fromValue("PIQI-PASS-FAIL")).isEmpty();
        assertThat(ScoringPolicyType.fromValue("")).isEmpty();
    }

    @Test
    @DisplayName("allowedValues lists exactly the documented slugs")
    void allowedValues() {
        assertThat(ScoringPolicyType.allowedValues())
                .containsExactly("piqi-dimension-scorecard", "piqi-check-scorecard", "piqi-pass-fail");
    }
}
