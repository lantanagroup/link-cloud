package com.lantanagroup.link.validation.enums;

import java.util.Arrays;
import java.util.List;
import java.util.Optional;

public enum RollupStrategy {
    WORST_OF("worst-of"),
    BEST_OF("best-of"),
    PASS_FAIL("pass-fail"),
    MAJORITY("majority"),
    ALL_MUST_PASS("all-must-pass");

    private final String value;

    RollupStrategy(String value) {
        this.value = value;
    }

    public String getValue() {
        return value;
    }

    public static Optional<RollupStrategy> fromValue(String value) {
        return Arrays.stream(values()).filter(r -> r.value.equals(value)).findFirst();
    }

    public static List<String> allowedValues() {
        return Arrays.stream(values()).map(RollupStrategy::getValue).toList();
    }
}
