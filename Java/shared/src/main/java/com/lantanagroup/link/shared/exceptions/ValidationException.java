package com.lantanagroup.link.shared.exceptions;

import lombok.Getter;

import java.util.List;

@Getter
public class ValidationException extends RuntimeException {
    private final List<String> errors;

    public ValidationException(List<String> errors) {
        this.errors = errors;
    }
    public ValidationException(String message) {
        super(message);
        this.errors = List.of(message);
    }
}
