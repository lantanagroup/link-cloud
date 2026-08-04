package com.lantanagroup.link.validation.exceptions;

public class RubricVersionConflictException extends RuntimeException {
    public RubricVersionConflictException(String rubricId, String semver) {
        super("Rubric " + rubricId + " v" + semver + " is already registered with a different definition; bump the version instead");
    }
}
