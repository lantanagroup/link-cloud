package com.lantanagroup.link.validation.exceptions;

public class RubricNotFoundException extends RuntimeException {
    public RubricNotFoundException(String rubricId) {
        super("Rubric not found: " + rubricId);
    }
}
