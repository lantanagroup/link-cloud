package com.lantanagroup.link.validation.exceptions;

import com.lantanagroup.link.validation.enums.RubricVersionStatus;

public class RubricVersionConflictException extends RuntimeException {
    public RubricVersionConflictException(String rubricId, String semver) {
        super("Rubric " + rubricId + " v" + semver + " is already registered with a different definition; bump the version instead");
    }

    public RubricVersionConflictException(String rubricId, String semver, RubricVersionStatus status) {
        super("Rubric " + rubricId + " v" + semver + " is " + status
                + " and its definition is immutable; bump the version instead");
    }
}
