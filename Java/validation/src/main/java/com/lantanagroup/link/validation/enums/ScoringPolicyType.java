package com.lantanagroup.link.validation.enums;

import java.util.Arrays;
import java.util.List;
import java.util.Optional;

public enum ScoringPolicyType {
    PIQI_DIMENSION_SCORECARD("piqi-dimension-scorecard"),
    PIQI_CHECK_SCORECARD("piqi-check-scorecard"),
    PIQI_PASS_FAIL("piqi-pass-fail");

    private final String value;

    ScoringPolicyType(String value) {
        this.value = value;
    }

    public String getValue() {
        return value;
    }

    public static Optional<ScoringPolicyType> fromValue(String value) {
        return Arrays.stream(values()).filter(t -> t.value.equals(value)).findFirst();
    }

    public static List<String> allowedValues() {
        return Arrays.stream(values()).map(ScoringPolicyType::getValue).toList();
    }
}
