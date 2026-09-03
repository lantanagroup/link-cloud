package com.lantanagroup.link.measureeval.exceptions;

/**
 * Thrown when the resource cache (Redis) is unreachable while reading a patient's resources.
 *
 * <p>Deliberately distinct from an empty read (cache reachable, but the key has no fields): an outage
 * means the resources are <em>unknown</em>, not absent. Surfacing this explicitly stops a cache outage
 * from masquerading downstream as "no resources" (a bogus not-reportable patient) or as a
 * {@code ResourceNotFoundException} when the subject is missing from a partial bundle.</p>
 *
 * <p>It is intentionally <b>not</b> part of the non-retryable/poison set — a cache outage is transient,
 * so records that fail this way are routed to {@code -Retry} and redelivered once Redis recovers.</p>
 */
public class ResourceCacheUnavailableException extends RuntimeException {

    public ResourceCacheUnavailableException(String message, Throwable cause) {
        super(message, cause);
    }
}
