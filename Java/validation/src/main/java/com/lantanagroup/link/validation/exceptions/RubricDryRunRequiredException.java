package com.lantanagroup.link.validation.exceptions;

public class RubricDryRunRequiredException extends RuntimeException {
    public RubricDryRunRequiredException(String rubricId, String semver) {
        super("Cannot publish " + rubricId + " v" + semver
                + ": no dry run has been completed for this version; a completed dry run "
                + "(any resulting status) is required before publish");
    }
}
