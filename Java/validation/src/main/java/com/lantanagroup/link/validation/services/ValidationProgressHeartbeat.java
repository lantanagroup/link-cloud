package com.lantanagroup.link.validation.services;

import org.slf4j.Logger;

import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

/**
 * Emits INFO lines while a long Validation step is in flight so Automation can
 * treat the run as progressing instead of a stall. Token must stay in lockstep
 * with {@code ValidationActivity.LogToken} in Automation.Link.
 */
public final class ValidationProgressHeartbeat implements AutoCloseable {
    public static final String LOG_TOKEN = "validation still in progress";
    static final long DEFAULT_INTERVAL_MS = 30_000L;

    private final Logger logger;
    private final String detail;
    private final long startedNanos = System.nanoTime();
    private final ScheduledExecutorService scheduler;

    private ValidationProgressHeartbeat(Logger logger, String detail, long intervalMs) {
        this.logger = logger;
        this.detail = detail;
        scheduler = Executors.newSingleThreadScheduledExecutor(r -> {
            Thread thread = new Thread(r, "validation-progress-heartbeat");
            thread.setDaemon(true);
            return thread;
        });
        long interval = Math.max(intervalMs, 1L);
        scheduler.scheduleAtFixedRate(this::tick, interval, interval, TimeUnit.MILLISECONDS);
    }

    public static ValidationProgressHeartbeat start(Logger logger, String detail) {
        return start(logger, detail, DEFAULT_INTERVAL_MS);
    }

    static ValidationProgressHeartbeat start(Logger logger, String detail, long intervalMs) {
        return new ValidationProgressHeartbeat(logger, detail, intervalMs);
    }

    static String format(String detail, long elapsedSeconds) {
        return LOG_TOKEN + ": " + detail + " (elapsed " + elapsedSeconds + "s)";
    }

    private void tick() {
        long elapsedSeconds = TimeUnit.NANOSECONDS.toSeconds(System.nanoTime() - startedNanos);
        logger.info(format(detail, elapsedSeconds));
    }

    @Override
    public void close() {
        scheduler.shutdownNow();
    }
}
