package com.lantanagroup.link.shared.kafka;

import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.common.header.Headers;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.listener.DeadLetterPublishingRecoverer;
import org.springframework.kafka.retrytopic.RetryTopicHeaders;

import java.math.BigInteger;
import java.nio.ByteBuffer;
import java.util.function.BiFunction;
import java.util.function.IntToLongFunction;

public class RetryTopicRecoverer implements ConsumerRecordRecoverer {

    private static final Logger logger = LoggerFactory.getLogger(RetryTopicRecoverer.class);

    /**
     * What is about to happen to a failed record. The destination resolver and this recoverer's log line
     * are both derived from this single value, so the log can never claim a retry that the resolver has
     * already decided against.
     */
    public enum Decision {
        /** Republished to the {@code -Retry} topic for another attempt. */
        RETRY,
        /** Dead-lettered: retries are switched off by configuration. */
        DLT_DISABLED,
        /** Dead-lettered: the exception can never succeed on retry. */
        DLT_POISON,
        /** Dead-lettered: no attempts remain. */
        DLT_EXHAUSTED
    }

    private final int maxAttempts;
    private final IntToLongFunction backoffMsForAttempt;
    private final DeadLetterPublishingRecoverer delegate;
    private final BiFunction<ConsumerRecord<?, ?>, Exception, Decision> decide;

    /**
     * @param maxAttempts         attempt at which a record is exhausted (used for logging)
     * @param backoffMsForAttempt maps a 1-based attempt number to the backoff delay in millis
     * @param delegate            publishes the record to the resolved {@code -Retry}/{@code -Error} topic
     * @param decide              the same decision the delegate's destination resolver applies; evaluated
     *                            after the attempts header is stamped so both see the identical count
     */
    public RetryTopicRecoverer(int maxAttempts,
                               IntToLongFunction backoffMsForAttempt,
                               DeadLetterPublishingRecoverer delegate,
                               BiFunction<ConsumerRecord<?, ?>, Exception, Decision> decide) {
        this.maxAttempts = maxAttempts;
        this.backoffMsForAttempt = backoffMsForAttempt;
        this.delegate = delegate;
        this.decide = decide;
    }

    /**
     * Attempts-only variant for callers with no poison classification or disable flag — where exhausting
     * the attempt budget genuinely is the only reason a record stops retrying. Prefer
     * {@link RetryTopicRecovererFactory#create} in production so the log reflects every routing reason.
     */
    public RetryTopicRecoverer(int maxAttempts, IntToLongFunction backoffMsForAttempt, DeadLetterPublishingRecoverer delegate) {
        this(maxAttempts, backoffMsForAttempt, delegate,
                (record, exception) -> currentAttempts(record.headers()) >= maxAttempts
                        ? Decision.DLT_EXHAUSTED
                        : Decision.RETRY);
    }

    @Override
    public void accept(ConsumerRecord<?, ?> record, Exception exception) {
        Headers headers = record.headers();

        // attempts is history: it records how many times the record ran, so it is stamped
        // unconditionally and survives onto a dead letter.
        int attempt = currentAttempts(headers) + 1;
        headers.remove(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS);
        headers.add(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS, ByteBuffer.allocate(4).putInt(attempt).array());

        if (headers.lastHeader(RetryTopicHeaders.DEFAULT_HEADER_ORIGINAL_TIMESTAMP) == null) {
            headers.add(RetryTopicHeaders.DEFAULT_HEADER_ORIGINAL_TIMESTAMP, BigInteger.valueOf(record.timestamp()).toByteArray());
        }

        // Exception headers are stamped by the DeadLetterPublishingRecoverer delegate on publish.
        // Evaluated here, after the attempts header is stamped, so this sees the same count the
        // delegate's destination resolver will read a moment later.
        Decision decision = decide.apply(record, exception);

        // The backoff timestamp is a due-time the -Retry listener waits on, so it is a promise that the
        // record will be delivered again. Stamp it only when that is true. A dead letter is never
        // redelivered, so a due-time on it describes a retry that was never scheduled — clear the one a
        // previous hop left rather than carrying it onto the DLT.
        headers.remove(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP);
        if (decision == Decision.RETRY) {
            long dueAt = System.currentTimeMillis() + backoffMsForAttempt.applyAsLong(attempt);
            headers.add(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP, BigInteger.valueOf(dueAt).toByteArray());
        }

        switch (decision) {
            case RETRY -> logger.info("Retry attempt {}/{} for topic [{}].",
                    attempt, maxAttempts, record.topic());
            case DLT_EXHAUSTED -> logger.warn("Max retry attempts ({}) reached for topic [{}]. Routing to DLT.",
                    maxAttempts, record.topic());
            case DLT_POISON -> logger.warn(
                    "Non-retryable exception [{}] for topic [{}]. Routing to DLT without retrying.",
                    exception == null ? "unknown" : exception.getClass().getName(), record.topic());
            case DLT_DISABLED -> logger.warn(
                    "Retry consumer is disabled. Routing record from topic [{}] to DLT without retrying.",
                    record.topic());
        }

        delegate.accept(record, exception);
    }

    /**
     * Retry attempts recorded in the attempts header, or 0 if absent. Shared with KafkaConfig's
     * destination resolver so routing and stamping use the same count.
     */
    public static int currentAttempts(Headers headers) {
        var header = headers.lastHeader(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS);
        if (header == null) return 0;
        return ByteBuffer.wrap(header.value()).getInt();
    }
}
