package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.common.header.Header;
import org.springframework.kafka.listener.ListenerExecutionFailedException;
import org.springframework.stereotype.Component;

import java.util.Locale;
import java.util.concurrent.atomic.AtomicLong;

/**
 * Publishes {@code isSuccess=false} only after {@code ReadyForValidation} has been dead-lettered.
 * A retryable attempt records its duration and publishes nothing. Deserialization failures carry no
 * report or patient id, and those records are not published.
 */
@Component
public class ValidationStageFailurePublisher {
    static final int MAX_ERROR_MESSAGE_LENGTH = 200;
    private static final String ELLIPSIS = "...";
    private static final String VALIDATION_FAILED = "Validation failed.";
    private static final String RETRY_COUNT_EXCEEDED = "retry count exceeded";
    /**
     * Case-insensitive substrings. A message that contains one is not safe to store.
     * {@code bearer } keeps the trailing space so the word alone is not treated as a secret.
     */
    private static final String[] SECRET_MARKERS = {
            "password=",
            "pwd=",
            "secret",
            "token",
            "apikey",
            "api_key",
            "accountkey",
            "sharedaccesssignature",
            "bearer "
    };
    /** Cause fqcn first: that is the failure under a framework wrapper when the header was stamped. */
    private static final String[] EXCEPTION_TYPE_HEADERS = {
            "kafka_dlt-exception-cause-fqcn",
            "kafka_exception-cause-fqcn",
            "kafka_dlt-exception-fqcn",
            "kafka_exception-fqcn"
    };
    private static final String[] EXCEPTION_MESSAGE_HEADERS = {
            Headers.EXCEPTION_MESSAGE,
            "kafka_dlt-exception-message",
            "kafka_exception-message"
    };

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
                stageErrorMessage(failure, record.headers()),
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
     * {@code <simple type>: <message>} for the failure that actually occurred. Framework wrappers are
     * unwrapped when they have a cause. The message is the first sanitized line. A blank or
     * secret-bearing message publishes the type only, or {@code Validation failed.} when no type survives.
     */
    private static String stageErrorMessage(Exception failure, org.apache.kafka.common.header.Headers headers) {
        Throwable cause = unwrap(failure);
        String typeName = cause != null ? simpleName(cause) : exceptionType(headers);
        String cleaned = cleanMessage(cause != null ? cause.getMessage() : exceptionMessage(headers));
        if (cleaned.isEmpty() || containsSecret(cleaned)) {
            return typeName != null ? typeName : VALIDATION_FAILED;
        }
        String message = limit(cleaned);
        return typeName != null ? typeName + ": " + message : message;
    }

    /**
     * ListenerExecutionFailedException and "Retry count exceeded" wrappers are the framework, not the
     * failure. Stop at the first throwable that is not one of those, or at a wrapper that has no cause.
     */
    private static Throwable unwrap(Throwable failure) {
        Throwable current = failure;
        int depth = 0;
        while (current != null && isFrameworkWrapper(current) && depth++ < 8) {
            Throwable next = current.getCause();
            if (next == null || next == current) {
                return current;
            }
            current = next;
        }
        return current;
    }

    private static boolean isFrameworkWrapper(Throwable failure) {
        if (failure instanceof ListenerExecutionFailedException) {
            return true;
        }
        String message = failure.getMessage();
        return message != null && message.toLowerCase(Locale.ROOT).contains(RETRY_COUNT_EXCEEDED);
    }

    private static String simpleName(Throwable failure) {
        String simple = failure.getClass().getSimpleName();
        if (simple == null || simple.isBlank()) {
            return simpleTypeName(failure.getClass().getName());
        }
        return simple;
    }

    private static String exceptionType(org.apache.kafka.common.header.Headers headers) {
        return simpleTypeName(firstHeader(headers, EXCEPTION_TYPE_HEADERS));
    }

    private static String simpleTypeName(String fqcnOrName) {
        if (fqcnOrName == null) {
            return null;
        }
        String value = fqcnOrName.trim();
        if (value.isEmpty()) {
            return null;
        }
        int dot = value.lastIndexOf('.');
        String simple = dot >= 0 && dot < value.length() - 1 ? value.substring(dot + 1) : value;
        return simple.isBlank() ? null : simple;
    }

    private static String exceptionMessage(org.apache.kafka.common.header.Headers headers) {
        return firstHeader(headers, EXCEPTION_MESSAGE_HEADERS);
    }

    private static String firstHeader(org.apache.kafka.common.header.Headers headers, String[] names) {
        if (headers == null) {
            return null;
        }
        for (String name : names) {
            Header header = headers.lastHeader(name);
            if (header == null || header.value() == null || header.value().length == 0) {
                continue;
            }
            String value = Headers.getString(header.value());
            if (!value.isBlank()) {
                return value;
            }
        }
        return null;
    }

    private static String cleanMessage(String raw) {
        if (raw == null || raw.isEmpty()) {
            return "";
        }
        int end = indexOfLineBreak(raw);
        StringBuilder cleaned = new StringBuilder(end);
        boolean pendingSpace = false;
        for (int i = 0; i < end; i++) {
            char c = raw.charAt(i);
            if (Character.isWhitespace(c)) {
                if (cleaned.length() > 0) {
                    pendingSpace = true;
                }
                continue;
            }
            if (Character.isISOControl(c)) {
                continue;
            }
            if (pendingSpace) {
                cleaned.append(' ');
                pendingSpace = false;
            }
            cleaned.append(c);
        }
        return cleaned.toString();
    }

    private static int indexOfLineBreak(String raw) {
        for (int i = 0; i < raw.length(); i++) {
            char c = raw.charAt(i);
            if (c == '\n' || c == '\r' || c == '\u2028' || c == '\u2029') {
                return i;
            }
        }
        return raw.length();
    }

    private static boolean containsSecret(String message) {
        String lower = message.toLowerCase(Locale.ROOT);
        for (String marker : SECRET_MARKERS) {
            if (lower.contains(marker)) {
                return true;
            }
        }
        return false;
    }

    private static String limit(String message) {
        if (message.length() <= MAX_ERROR_MESSAGE_LENGTH) {
            return message;
        }
        return message.substring(0, MAX_ERROR_MESSAGE_LENGTH - ELLIPSIS.length()) + ELLIPSIS;
    }

    private static boolean isBlank(String value) {
        return value == null || value.isBlank();
    }
}
