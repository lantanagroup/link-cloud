package com.lantanagroup.link.validation.exceptions;

import java.util.List;

public class InvalidRubricDefinitionException extends RuntimeException {

    private final List<String> errors;

    public InvalidRubricDefinitionException(String message, List<String> errors) {
        super(message);
        this.errors = errors;
    }

    public List<String> getErrors() {
        return errors;
    }
}
