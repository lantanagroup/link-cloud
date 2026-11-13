package com.lantanagroup.link.validation.services.execution;

import com.lantanagroup.link.validation.models.RawFinding;

import java.util.List;

/**
 * Result of running one rubric check. Each check executes independently, and the orchestrator
 * merges all outcomes in the original check order on the request thread.
 * {@code findings} also contains execution errors converted into {@code check-execution-error}
 * findings, allowing successful and failed checks to be handled uniformly.
 * @param checkLocalId the originating check's local ID
 * @param findings the findings produced by the check; empty means it passed
 * @param durationMs execution time in milliseconds
 */
public record CheckOutcome(String checkLocalId, List<RawFinding> findings, double durationMs) {
}
