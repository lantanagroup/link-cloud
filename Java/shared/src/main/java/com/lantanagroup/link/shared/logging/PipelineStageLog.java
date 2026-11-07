package com.lantanagroup.link.shared.logging;

import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.common.header.Header;
import org.apache.kafka.common.record.TimestampType;
import org.slf4j.Logger;
import org.slf4j.MDC;

import java.nio.charset.StandardCharsets;
import java.util.Collection;
import java.util.Objects;
import java.util.stream.Collectors;

/**
 * The per-patient record of one pipeline stage handling one message; the Java counterpart of the .NET
 * {@code PipelineStageLog}, writing the same line. Open it with try-with-resources when a stage starts
 * on a message. While open it puts the message's ids in the MDC key {@value #MDC_KEY}, which the Loki
 * and console patterns append to every line, so every line the stage writes can be found by report or
 * patient. On close it writes one INFO line (WARN when the stage failed) saying how the stage ended, how
 * long the message waited in Kafka, and how long the stage took.
 *
 * <p>The line's shape is stable on purpose: it is what a per-stage timing query reads, and the .NET
 * services write the same one. It is written once per patient per stage, never per resource. Call
 * {@link #setOutcome} on every path; a record closed without one is logged as {@code failed}.
 */
public final class PipelineStageLog implements AutoCloseable {

    /** The MDC key holding the message's ids, appended to log lines by the logback patterns. */
    public static final String MDC_KEY = "link";

    public static final String COMPLETED = "completed";
    public static final String SKIPPED = "skipped";
    public static final String FAILED = "failed";

    private final Logger logger;
    private final String stage;
    private final String facilityId;
    private final String patientId;
    private final String correlationId;
    private final String reportIds;
    private final String phase;
    private final String waitedMs;
    private final long startedNanos = System.nanoTime();
    private final String previousMdc;
    private String outcome;
    private boolean closed;

    private PipelineStageLog(Logger logger, String stage, String facilityId, String patientId, String correlationId,
                             Collection<String> reportIds, String phase, Long producedEpochMs) {
        this.logger = logger;
        this.stage = clean(stage);
        this.facilityId = clean(facilityId);
        this.patientId = clean(patientId);
        this.correlationId = clean(correlationId);
        String joined = reportIds == null ? "" : reportIds.stream()
                .filter(Objects::nonNull).filter(id -> !id.isBlank())
                .map(PipelineStageLog::clean).distinct().collect(Collectors.joining(","));
        this.reportIds = joined.isEmpty() ? "-" : joined;
        this.phase = normalizePhase(phase);
        this.waitedMs = producedEpochMs != null && producedEpochMs > 0
                ? Long.toString(Math.max(0, System.currentTimeMillis() - producedEpochMs))
                : "-";

        this.previousMdc = MDC.get(MDC_KEY);
        MDC.put(MDC_KEY, String.format(" [facility=%s patient=%s correlation=%s reports=%s]",
                this.facilityId, this.patientId, this.correlationId, this.reportIds));
    }

    /**
     * Starts the record for a stage handling a Kafka record. The record's create time (set by the
     * producer) gives how long it waited in Kafka.
     */
    public static PipelineStageLog start(Logger logger, String stage, ConsumerRecord<?, ?> record, String facilityId,
                                         String patientId, String correlationId, Collection<String> reportIds,
                                         String phase) {
        Long produced = record != null && record.timestampType() == TimestampType.CREATE_TIME ? record.timestamp() : null;
        return new PipelineStageLog(logger, stage, facilityId, patientId, correlationId, reportIds, phase, produced);
    }

    /**
     * A stage name that includes its query phase, as the Report and Patient State Machine ADR names its
     * statuses: {@code InitialEvaluation}, {@code SupplementalEvaluation} and so on. A phase other than
     * initial or supplemental (or none) leaves the name as it is.
     */
    public static String phasedStage(String stage, String phase) {
        if ("initial".equalsIgnoreCase(phase)) {
            return "Initial" + stage;
        }
        if ("supplemental".equalsIgnoreCase(phase)) {
            return "Supplemental" + stage;
        }
        return stage;
    }

    /** The last value of a Kafka header as text, or null when it is absent; for logging only. */
    public static String header(ConsumerRecord<?, ?> record, String name) {
        if (record == null || record.headers() == null) {
            return null;
        }
        Header header = record.headers().lastHeader(name);
        return header == null || header.value() == null ? null : new String(header.value(), StandardCharsets.UTF_8);
    }

    /** How the stage ended: {@link #COMPLETED}, {@link #SKIPPED} or {@link #FAILED}. */
    public void setOutcome(String outcome) {
        this.outcome = outcome;
    }

    @Override
    public void close() {
        if (closed) {
            return;
        }
        closed = true;
        long tookMs = (System.nanoTime() - startedNanos) / 1_000_000;
        String finalOutcome = outcome == null ? FAILED : outcome;
        String template = "Pipeline stage {} {}: facility {}, patient {}, correlation {}, reports {}, phase {}, waited {} ms, took {} ms";
        Object[] args = {stage, finalOutcome, facilityId, patientId, correlationId, reportIds, phase, waitedMs, tookMs};
        if (FAILED.equals(finalOutcome)) {
            logger.warn(template, args);
        } else {
            logger.info(template, args);
        }

        if (previousMdc == null) {
            MDC.remove(MDC_KEY);
        } else {
            MDC.put(MDC_KEY, previousMdc);
        }
    }

    /**
     * The query phases arrive as {@code Initial}, {@code INITIAL} or {@code SUPPLEMENTAL} depending on the
     * service; they are written in lower case so one query matches every stage. Other phase values keep
     * their case.
     */
    private static String normalizePhase(String phase) {
        String cleaned = clean(phase);
        return "initial".equalsIgnoreCase(cleaned) || "supplemental".equalsIgnoreCase(cleaned)
                ? cleaned.toLowerCase(java.util.Locale.ROOT)
                : cleaned;
    }

    /** Replaces control characters, so a value can't forge a log line. */
    private static String clean(String value) {
        if (value == null || value.isBlank()) {
            return "-";
        }
        StringBuilder builder = new StringBuilder(value.length());
        for (char ch : value.toCharArray()) {
            builder.append(ch < 32 || ch == 127 ? ' ' : ch);
        }
        return builder.toString();
    }
}
