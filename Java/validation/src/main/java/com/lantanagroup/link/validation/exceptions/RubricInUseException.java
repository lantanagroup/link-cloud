package com.lantanagroup.link.validation.exceptions;

/** A lifecycle change refused because the reporting pipeline would be left with no rubric version to run. */
public class RubricInUseException extends RuntimeException {
    public RubricInUseException(String rubricId, String semver) {
        super("Cannot retire " + rubricId + " v" + semver + ": it is the only published version of the rubric "
                + "the reporting pipeline validates against, and retiring it would leave validation nothing to run. "
                + "Publish another version first");
    }
}
