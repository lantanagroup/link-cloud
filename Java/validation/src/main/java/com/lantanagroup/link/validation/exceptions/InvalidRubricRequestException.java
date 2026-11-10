package com.lantanagroup.link.validation.exceptions;

/**
 * A request to the rubric registry asked for something it does not offer, such as sorting the list by a
 * property that cannot be sorted on. Distinct from {@link InvalidRubricDefinitionException}: nothing is
 * wrong with any rubric.
 */
public class InvalidRubricRequestException extends RuntimeException {
    public InvalidRubricRequestException(String message) {
        super(message);
    }
}
