package com.lantanagroup.link.shared.utils;

import java.util.List;

/**
 * Explicit histogram buckets for stage-duration instruments (milliseconds).
 * Default OTel buckets top out near 10 s; EHR Observation queries are measured at ~23 s.
 */
public final class HistogramBuckets {
    public static final List<Long> DURATION_MS_LONG = List.of(
            1L, 2L, 5L, 10L, 25L, 50L, 100L, 250L, 500L,
            1000L, 2500L, 5000L, 10000L, 15000L, 30000L, 45000L, 60000L);

    public static final List<Double> DURATION_MS_DOUBLE = List.of(
            1.0, 2.0, 5.0, 10.0, 25.0, 50.0, 100.0, 250.0, 500.0,
            1000.0, 2500.0, 5000.0, 10000.0, 15000.0, 30000.0, 45000.0, 60000.0);

    private HistogramBuckets() {
    }
}
