package com.lantanagroup.link.validation.exceptions;


import com.lantanagroup.link.validation.enums.RubricVersionStatus;

public class RubricLifecycleException extends RuntimeException {
    public RubricLifecycleException(String rubricId, String semver, RubricVersionStatus from, String action) {
        super("Cannot " + action + " " + rubricId + " v" + semver + ": status is " + from);
    }
}
