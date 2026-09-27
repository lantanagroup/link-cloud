package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.utils.LogUtils;
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
    private final String facilityId;
    private final String reportId;
    private final long startedNanos = System.nanoTime();
    private final ScheduledExecutorService scheduler;

    private ValidationProgressHeartbeat(Logger logger, String detail, String facilityId, String reportId, long intervalMs) {
        this.logger = logger;
        this.detail = detail;
        this.facilityId = facilityId;
        this.reportId = reportId;
        scheduler = Executors.newSingleThreadScheduledExecutor(r -> {
            Thread thread = new Thread(r, "validation-progress-heartbeat");
            thread.setDaemon(true);
            return thread;
        });
        long interval = Math.max(intervalMs, 1L);
        scheduler.scheduleAtFixedRate(this::tick, interval, interval, TimeUnit.MILLISECONDS);
    }

    public static ValidationProgressHeartbeat start(Logger logger, String detail) {
        return start(logger, detail, null, null, DEFAULT_INTERVAL_MS);
    }

    public static ValidationProgressHeartbeat start(Logger logger, String detail, String facilityId, String reportId) {
        return start(logger, detail, facilityId, reportId, DEFAULT_INTERVAL_MS);
    }

    static ValidationProgressHeartbeat start(Logger logger, String detail, String facilityId, String reportId, long intervalMs) {
        return new ValidationProgressHeartbeat(logger, detail, facilityId, reportId, intervalMs);
    }

    static String format(String detail, String facilityId, String reportId, long elapsedSeconds) {
        StringBuilder line = new StringBuilder(LOG_TOKEN).append(": ").append(detail);
        String safeFacility = LogUtils.sanitize(facilityId);
        String safeReport = LogUtils.sanitize(reportId);
        if (safeFacility != null && !safeFacility.isBlank())
            line.append(" facility=").append(safeFacility);
        if (safeReport != null && !safeReport.isBlank())
            line.append(" report=").append(safeReport);
        line.append(" (elapsed ").append(elapsedSeconds).append("s)");
        return line.toString();
    }

    static String format(String detail, long elapsedSeconds) {
        return format(detail, null, null, elapsedSeconds);
    }

    private void tick() {
        long elapsedSeconds = TimeUnit.NANOSECONDS.toSeconds(System.nanoTime() - startedNanos);
        logger.info(format(detail, facilityId, reportId, elapsedSeconds));
    }

    @Override
    public void close() {
        scheduler.shutdownNow();
    }
}
