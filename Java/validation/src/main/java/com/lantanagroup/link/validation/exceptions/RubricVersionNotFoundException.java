package com.lantanagroup.link.validation.exceptions;

public class RubricVersionNotFoundException extends RuntimeException {
    public RubricVersionNotFoundException(String rubricId, String semver) {
        super("Rubric version not found: " + rubricId + " " + semver);
    }
}
