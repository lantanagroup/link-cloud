package com.lantanagroup.link.validation.exceptions;

/**
 * A draft could not be saved because it is not in the state the caller said it would be: someone else
 * changed or deleted it since the caller read it, or it exists when the caller meant to create it.
 */
public class RubricPreconditionFailedException extends RuntimeException {

    private RubricPreconditionFailedException(String message) {
        super(message);
    }

    /** The draft's definition is not the one the caller last read. */
    public static RubricPreconditionFailedException changed(String rubricId, String semver) {
        return new RubricPreconditionFailedException("Rubric " + rubricId + " v" + semver
                + " has been changed since it was read; read it again before saving");
    }

    /** The caller read a draft that is no longer there. */
    public static RubricPreconditionFailedException missing(String rubricId, String semver) {
        return new RubricPreconditionFailedException("Rubric " + rubricId + " v" + semver
                + " no longer exists; it was deleted since it was read");
    }

    /** The caller meant to create a version that already exists. */
    public static RubricPreconditionFailedException alreadyExists(String rubricId, String semver) {
        return new RubricPreconditionFailedException("Rubric " + rubricId + " v" + semver
                + " already exists; choose another version or open the existing one");
    }
}
