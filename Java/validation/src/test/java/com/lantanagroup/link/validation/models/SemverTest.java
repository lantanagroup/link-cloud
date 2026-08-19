package com.lantanagroup.link.validation.models;

import com.lantanagroup.link.validation.entities.RubricVersion;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.junit.jupiter.params.provider.ValueSource;

import java.util.ArrayList;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

class SemverTest {

    @ParameterizedTest
    @ValueSource(strings = {"1.0.0", "0.0.1", "10.20.30", "1.0.1753017600000"})
    @DisplayName("valid semantic versions accepted")
    void validVersions(String value) {
        assertThat(Semver.isValid(value)).isTrue();
    }

    @ParameterizedTest
    @ValueSource(strings = {"v1", "1.0", "1", "1.0.0.0", "1.0.0-", "1.0.0-alpha", "1.0.0-rc.1",
            "01a.0.0", "one.two.three", ""})
    @DisplayName("invalid semantic versions rejected (pre-release tags no longer accepted)")
    void invalidVersions(String value) {
        assertThat(Semver.isValid(value)).isFalse();
    }

    @Test
    @DisplayName("null is invalid and parse throws")
    void nullVersion() {
        assertThat(Semver.isValid(null)).isFalse();
        assertThatThrownBy(() -> Semver.parse(null)).isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    @DisplayName("orders numerically, not lexically (1.9.0 < 1.10.0)")
    void numericOrdering() {
        assertThat(Semver.parse("1.9.0")).isLessThan(Semver.parse("1.10.0"));
        assertThat(Semver.parse("2.0.0")).isGreaterThan(Semver.parse("1.99.99"));
    }

    @Test
    @DisplayName("isZero recognizes 0.0.0 even with leading zeros; parse/isValid still accept it syntactically")
    void zeroVersionDetection() {
        assertThat(Semver.parse("0.00.0").isZero()).isTrue();
        assertThat(Semver.parse("1.0.0").isZero()).isFalse();
    }

    @Test
    @DisplayName("versionComparator sorts unparseable semvers lowest instead of throwing")
    void comparatorHandlesGarbage() {
        List<RubricVersion> versions = new ArrayList<>(List.of(
                RubricVersion.builder().semver("1.10.0").build(),
                RubricVersion.builder().semver("garbage").build(),
                RubricVersion.builder().semver("1.9.0").build()
        ));
        versions.sort(Semver.versionComparator());
        assertThat(versions).extracting(RubricVersion::getSemver)
                .containsExactly("garbage", "1.9.0", "1.10.0");
    }

    @ParameterizedTest
    @CsvSource({
            "01.02.03,   1.2.3",
            "1.2.3,       1.2.3",
            "001.0.0,     1.0.0"
    })
    @DisplayName("toCanonicalString strips leading zeros")
    void toCanonicalStringStripsLeadingZeros(String input, String canonical) {
        assertThat(Semver.parse(input).toCanonicalString()).isEqualTo(canonical);
    }

    @Test
    @DisplayName("parse rejects a component that overflows long, with a clear message")
    void parseRejectsOverflowingComponent() {
        String tooBig = "99999999999999999999.0.0";
        assertThat(Semver.isValid(tooBig)).isFalse();
        assertThatThrownBy(() -> Semver.parse(tooBig))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining(String.valueOf(Long.MAX_VALUE));
    }

    @ParameterizedTest
    @CsvSource({
            "01.2.3,        1.2.3",
            "1.2.3,         1.2.3",
            "1.02.0,        1.2.0"
    })
    @DisplayName("normalize canonicalizes a parseable semver and is idempotent")
    void normalizeCanonicalizes(String input, String expected) {
        assertThat(Semver.normalize(input)).isEqualTo(expected);
        // normalizing an already-canonical value returns it unchanged
        assertThat(Semver.normalize(expected)).isEqualTo(expected);
    }

    @ParameterizedTest
    @ValueSource(strings = {"v1", "1.0", "not-a-semver", ""})
    @DisplayName("normalize passes an unparseable value through unchanged, so it still 404s/400s downstream")
    void normalizeFallsBackForUnparseable(String input) {
        assertThat(Semver.normalize(input)).isEqualTo(input);
    }

    @Test
    @DisplayName("normalize(null) returns null rather than throwing")
    void normalizeNull() {
        assertThat(Semver.normalize(null)).isNull();
    }
}
