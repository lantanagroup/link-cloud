package com.lantanagroup.link.validation.enums;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class RollupStrategyTest {

    @Test
    @DisplayName("resolves each documented slug")
    void fromValueResolvesSlugs() {
        assertThat(RollupStrategy.fromValue("worst-of")).contains(RollupStrategy.WORST_OF);
        assertThat(RollupStrategy.fromValue("best-of")).contains(RollupStrategy.BEST_OF);
        assertThat(RollupStrategy.fromValue("pass-fail")).contains(RollupStrategy.PASS_FAIL);
        assertThat(RollupStrategy.fromValue("majority")).contains(RollupStrategy.MAJORITY);
        assertThat(RollupStrategy.fromValue("all-must-pass")).contains(RollupStrategy.ALL_MUST_PASS);
    }

    @Test
    @DisplayName("unknown and wrong-case slugs resolve empty")
    void fromValueRejectsUnknown() {
        assertThat(RollupStrategy.fromValue("average")).isEmpty();
        assertThat(RollupStrategy.fromValue("WORST-OF")).isEmpty();
        assertThat(RollupStrategy.fromValue("")).isEmpty();
    }

    @Test
    @DisplayName("allowedValues lists exactly the documented slugs")
    void allowedValues() {
        assertThat(RollupStrategy.allowedValues())
                .containsExactly("worst-of", "best-of", "pass-fail", "majority", "all-must-pass");
    }
}
