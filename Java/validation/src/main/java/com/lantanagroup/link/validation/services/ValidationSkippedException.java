package com.lantanagroup.link.validation.services;

/**
 * Thrown when FHIR validation is skipped due to a known upstream bug
 * (e.g., HAPI FHIR issue #7200 / HAPI-2509) rather than completing
 * normally or failing for a legitimate reason.
 */
public class ValidationSkippedException extends RuntimeException {
    public ValidationSkippedException(String message, Throwable cause) {
        super(message, cause);
    }
}
