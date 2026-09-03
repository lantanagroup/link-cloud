package com.lantanagroup.link.validation.services.execution;

import com.lantanagroup.link.validation.models.RawFinding;

import java.util.List;

/**
 * The immutable result of running one rubric check.
 *
 * <p>Each check runs as an independent task and returns one of these. The orchestrator collects
 * all outcomes after every task completes and merges them on the request thread, in the original
 * check order, so no worker thread ever writes to shared state.
 *
 * <p>{@code findings} already includes any per-check execution error: the task catches its own
 * exceptions and turns them into a {@code check-execution-error} finding, so the merge step treats
 * success and failure uniformly.
 *
 * @param checkLocalId the originating check's local id (used to key per-check durations)
 * @param findings     the findings this check produced (empty means the check passed)
 * @param durationMs    wall-clock time this check took, in milliseconds (sub-millisecond precision,
 *                      rounded to 2 decimal places; nanoTime-based since most checks run well under 1ms)
 */
public record CheckOutcome(String checkLocalId, List<RawFinding> findings, double durationMs) {
}
