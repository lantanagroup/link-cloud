package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.springframework.stereotype.Component;

import java.util.concurrent.atomic.AtomicLong;

/**
 * Publishes {@code isSuccess=false} only after {@code ReadyForValidation} has been dead-lettered.
 * A retryable attempt records its duration and publishes nothing. Deserialization failures carry no
 * report or patient id, and those records are not published.
 */
@Component
public class ValidationStageFailurePublisher {
    private final ReportStatusUpdatedProducer reportStatusUpdatedProducer;
    /** Duration of the attempt currently failing, or -1 when this attempt has not failed. */
    private final AtomicLong failedAttemptMillis = new AtomicLong(-1);

    public ValidationStageFailurePublisher(ReportStatusUpdatedProducer reportStatusUpdatedProducer) {
        this.reportStatusUpdatedProducer = reportStatusUpdatedProducer;
    }

    public void clearFailedAttempt() {
        failedAttemptMillis.set(-1);
    }

    public void noteFailedAttempt(long processingTimeMillis) {
        failedAttemptMillis.set(Math.max(processingTimeMillis, 0L));
    }

    /**
     * Dead-letter hook for {@code readyForValidationRecoverer}. Invoked only after the record is
     * durably on {@code ReadyForValidation-Error}, never when it is routed to {@code -Retry}.
     */
    public void publishFinalFailure(ConsumerRecord<?, ?> record, Exception failure) {
        Long processingTime = takeFailedAttempt();
        if (!(record.value() instanceof ReadyForValidation ready)) {
            return;
        }
        String reportId = ready.getReportTrackingId();
        String patientId = ready.getPatientId();
        if (isBlank(reportId) || isBlank(patientId)) {
            return;
        }
        reportStatusUpdatedProducer.publishFailure(
                reportId,
                patientId,
                correlationId(record),
                processingTime,
                stageErrorMessage(failure),
                record.headers());
    }

    private Long takeFailedAttempt() {
        long noted = failedAttemptMillis.getAndSet(-1);
        return noted < 0 ? null : noted;
    }

    private static String correlationId(ConsumerRecord<?, ?> record) {
        String correlationId = Headers.getCorrelationId(record.headers());
        if ((correlationId == null || correlationId.isBlank())
                && record.key() instanceof ReadyForValidation.Key key) {
            correlationId = key.getCorrelationId();
        }
        return correlationId;
    }

    /**
     * The exception type only. Provider and parser messages can include connection text,
     * SQL, or a fragment of the payload, and this event is what Report stores.
     */
    private static String stageErrorMessage(Exception failure) {
        if (failure == null) {
            return "Exception";
        }
        String name = failure.getClass().getSimpleName();
        return name == null || name.isBlank() ? failure.getClass().getName() : name;
    }

    private static boolean isBlank(String value) {
        return value == null || value.isBlank();
    }
}
