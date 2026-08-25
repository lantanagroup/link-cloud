package com.lantanagroup.link.shared.kafka;

import com.lantanagroup.link.shared.config.KafkaRetryConfig;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.common.TopicPartition;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.DeadLetterPublishingRecoverer;

import java.util.Set;
import java.util.function.BiConsumer;
import java.util.function.BiFunction;

/**
 * Builds a {@link RetryTopicRecoverer} wired to a destination resolver that routes a failed record to
 * the {@code -Retry} topic (transient, attempts remaining) or the {@code -Error} topic (retries
 * disabled, poison, or attempts exhausted).
 *
 * <p>The poison classification is parameterized via {@code nonRetryable} rather than hard-coded, so
 * this stays free of service-specific (e.g. FHIR/HAPI) exception types — each service supplies the
 * set of exceptions that can never succeed on retry from its own classpath.</p>
 */
public final class RetryTopicRecovererFactory {

    private RetryTopicRecovererFactory() {
    }

    public static RetryTopicRecoverer create(
            KafkaTemplate<?, ?> kafkaTemplate,
            String retryTopic,
            String errorTopic,
            KafkaRetryConfig config,
            Set<Class<? extends Throwable>> nonRetryable) {
        return create(kafkaTemplate, retryTopic, errorTopic, config, nonRetryable, null);
    }

    /**
     * @param kafkaTemplate template used to publish to the resolved topic
     * @param retryTopic    destination for transient failures with attempts remaining
     * @param errorTopic    destination (dead-letter) for disabled/poison/exhausted failures
     * @param config        retry policy (attempt count, per-attempt backoff, disable flag)
     * @param nonRetryable  exception types that are poison anywhere in the cause chain
     * @param onDeadLetter  optional hook run after a record is durably dead-lettered (never on retry) —
     *                      the safe moment to release the record's side resources; may be null
     */
    public static RetryTopicRecoverer create(
            KafkaTemplate<?, ?> kafkaTemplate,
            String retryTopic,
            String errorTopic,
            KafkaRetryConfig config,
            Set<Class<? extends Throwable>> nonRetryable,
            BiConsumer<ConsumerRecord<?, ?>, Exception> onDeadLetter) {

        // The single routing decision. Both the destination resolver below and the recoverer's log line
        // are derived from this, so a record can never be dead-lettered while the log reports a retry.
        // Route straight to the error topic when retries are disabled, the message is poison (malformed
        // content / deserialization can never succeed), or attempts are exhausted.
        BiFunction<ConsumerRecord<?, ?>, Exception, RetryTopicRecoverer.Decision> decide =
                (record, exception) -> {
                    if (config.isDisableRetryConsumer()) {
                        return RetryTopicRecoverer.Decision.DLT_DISABLED;
                    }
                    if (isNonRetryable(exception, nonRetryable)) {
                        return RetryTopicRecoverer.Decision.DLT_POISON;
                    }
                    int attempt = RetryTopicRecoverer.currentAttempts(record.headers());
                    if (attempt >= config.effectiveMaxAttempts()) {
                        return RetryTopicRecoverer.Decision.DLT_EXHAUSTED;
                    }
                    return RetryTopicRecoverer.Decision.RETRY;
                };

        BiFunction<ConsumerRecord<?, ?>, Exception, TopicPartition> resolver =
                (record, exception) -> {
                    String target = decide.apply(record, exception) == RetryTopicRecoverer.Decision.RETRY
                            ? retryTopic
                            : errorTopic;
                    return new TopicPartition(target, record.partition());
                };

        // Relies on spring-kafka's failIfSendResultIsError=true default (since 2.7): accept() awaits
        // the send result (bounded by the producer's delivery.timeout.ms + buffer) and throws on
        // failure, so the source offset stays uncommitted and the onDeadLetter hook only runs after a
        // durable publish. Pinned by RetryTopicRecovererDecisionTest.failedDeadLetterPublish_*.
        DeadLetterPublishingRecoverer delegate = new DeadLetterPublishingRecoverer(kafkaTemplate, resolver);

        return new RetryTopicRecoverer(
                config.effectiveMaxAttempts(),
                config::backoffMsForAttempt,
                delegate,
                decide,
                onDeadLetter);
    }

    /**
     * Poison classification: returns true if any exception in the cause chain is an instance of one of
     * the configured non-retryable types. Public so callers/tests can reuse the exact classification
     * the resolver applies.
     */
    public static boolean isNonRetryable(Throwable t, Set<Class<? extends Throwable>> nonRetryable) {
        Throwable cause = t;
        while (cause != null) {
            for (Class<? extends Throwable> type : nonRetryable) {
                if (type.isInstance(cause)) {
                    return true;
                }
            }
            Throwable next = cause.getCause();
            cause = (next == cause) ? null : next;
        }
        return false;
    }
}
