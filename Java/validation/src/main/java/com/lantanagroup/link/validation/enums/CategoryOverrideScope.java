package com.lantanagroup.link.validation.enums;

public enum CategoryOverrideScope {

    /** Every finding, regardless of which check type produced it. */
    ALL_CHECKS,

    /** Only findings emitted by {@link CheckType#FHIR_CONFORMANCE} checks. */
    FHIR_ONLY;

    public boolean includes(CheckType checkType) {
        return switch (this) {
            case ALL_CHECKS -> true;
            case FHIR_ONLY -> checkType == CheckType.FHIR_CONFORMANCE;
        };
    }
}
