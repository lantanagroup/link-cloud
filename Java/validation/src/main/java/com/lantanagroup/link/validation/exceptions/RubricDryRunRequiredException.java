package com.lantanagroup.link.validation.exceptions;

import com.lantanagroup.link.validation.enums.RubricResultStatus;

public class RubricDryRunRequiredException extends RuntimeException {
    public RubricDryRunRequiredException(String rubricId, String semver, RubricResultStatus dryRunStatus) {
        super("Cannot publish " + rubricId + " v" + semver + ": "
                + (dryRunStatus == null
                        ? "no dry run has been completed for this version"
                        : "dry run status is " + dryRunStatus)
                + "; a dry run with status ACCEPTABLE or ACCEPTABLE_WITH_WARNINGS is required");
    }
}
