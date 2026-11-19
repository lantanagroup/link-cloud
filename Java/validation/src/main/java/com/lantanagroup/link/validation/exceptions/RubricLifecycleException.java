package com.lantanagroup.link.validation.exceptions;


import com.lantanagroup.link.validation.enums.RubricVersionStatus;

public class RubricLifecycleException extends RuntimeException {
    public RubricLifecycleException(String rubricId, String semver, RubricVersionStatus from, String action) {
        super("Cannot " + action + " " + rubricId + " v" + semver + ": status is " + from);
    }

    private RubricLifecycleException(String message) {
        super(message);
    }

    /** The draft's definition was replaced while it was being published, so what was checked is not what would be published. */
    public static RubricLifecycleException changedWhilePublishing(String rubricId, String semver) {
        return new RubricLifecycleException("Cannot publish " + rubricId + " v" + semver
                + ": its definition was changed while it was being published; review it, and dry-run it if required, then publish again");
    }
}
