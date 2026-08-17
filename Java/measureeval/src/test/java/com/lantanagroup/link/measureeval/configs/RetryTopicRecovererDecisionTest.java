package com.lantanagroup.link.measureeval.configs;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import com.lantanagroup.link.shared.config.KafkaRetryConfig;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.kafka.RetryTopicRecoverer;
import com.lantanagroup.link.shared.kafka.RetryTopicRecovererFactory;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.Node;
import org.apache.kafka.common.PartitionInfo;
import org.apache.kafka.common.header.Header;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.retrytopic.RetryTopicHeaders;

import java.math.BigInteger;
import java.nio.ByteBuffer;
import java.time.Duration;
import java.util.Collections;
import java.util.List;
import java.util.concurrent.CompletableFuture;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.*;

/**
 * Everything the recoverer derives from its routing decision — the topic, the log line, and the retry
 * headers — must agree with each other.
 *
 * <p>{@link RetryTopicRecoverer} emits the log and stamps the headers, but the destination is chosen by
 * the resolver built in {@link RetryTopicRecovererFactory}, which considers three reasons to
 * dead-letter: retries disabled, poison exception, attempts exhausted. Anything derived from only one of
 * those three describes a retry that never happens for the other two.</p>
 *
 * <p>These tests drive the real production wiring and assert the log and the published headers against
 * the topic the record actually went to, so the three can never drift apart again.</p>
 */
class RetryTopicRecovererDecisionTest {

    private static final String RETRY_TOPIC = "ResourcesNormalized-Retry";
    private static final String ERROR_TOPIC = "ResourcesNormalized-Error";

    private Logger recovererLogger;
    private ListAppender<ILoggingEvent> appender;
    private Level originalLevel;

    @BeforeEach
    void attachAppender() {
        recovererLogger = (Logger) LoggerFactory.getLogger(RetryTopicRecoverer.class);
        originalLevel = recovererLogger.getLevel();
        recovererLogger.setLevel(Level.TRACE);
        appender = new ListAppender<>();
        appender.start();
        recovererLogger.addAppender(appender);
    }

    @AfterEach
    void detachAppender() {
        recovererLogger.detachAppender(appender);
        appender.stop();
        recovererLogger.setLevel(originalLevel);
    }

    /** What the recoverer did with a record: what it published, and what it said about it. */
    private record Outcome(ProducerRecord<Object, Object> published, ILoggingEvent log) {
        String topic() {
            return published.topic();
        }

        String message() {
            return log.getFormattedMessage();
        }

        Header header(String name) {
            return published.headers().lastHeader(name);
        }
    }

    private static ConsumerRecord<String, String> record() {
        return new ConsumerRecord<>("ResourcesNormalized", 0, 0L, "key", "value");
    }

    /** Pre-stamp the attempts header as if {@code attempts} retries already occurred. */
    private static void stampAttempts(ConsumerRecord<?, ?> record, int attempts) {
        record.headers().add(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS,
                ByteBuffer.allocate(Integer.BYTES).putInt(attempts).array());
    }

    private static KafkaRetryConfig retryConfig(int maxAttempts, boolean disableRetryConsumer) {
        KafkaRetryConfig config = new KafkaRetryConfig();
        // effectiveMaxAttempts() == size + 1, so (maxAttempts - 1) delays yields exactly maxAttempts attempts.
        config.setConsumerRetryDuration(Collections.nCopies(Math.max(maxAttempts - 1, 0), Duration.ofSeconds(1)));
        config.setDisableRetryConsumer(disableRetryConsumer);
        return config;
    }

    /**
     * Drives a failed record through the real {@link RetryTopicRecovererFactory#create} wiring with the
     * production poison set, and captures both the topic the resolver published to and the single log
     * line the recoverer emitted.
     */
    @SuppressWarnings("unchecked")
    private Outcome run(KafkaRetryConfig config, ConsumerRecord<String, String> record, Exception exception) {
        KafkaTemplate<Object, Object> template = mock(KafkaTemplate.class);
        when(template.partitionsFor(anyString())).thenAnswer(invocation ->
                List.of(new PartitionInfo(invocation.getArgument(0), 0, null, new Node[0], new Node[0])));
        when(template.send(any(ProducerRecord.class)))
                .thenReturn(CompletableFuture.completedFuture(null));

        RetryTopicRecoverer recoverer = RetryTopicRecovererFactory.create(
                template, RETRY_TOPIC, ERROR_TOPIC, config, KafkaConfig.NON_RETRYABLE);
        recoverer.accept(record, exception);

        ArgumentCaptor<ProducerRecord<Object, Object>> captor = ArgumentCaptor.forClass(ProducerRecord.class);
        verify(template).send(captor.capture());

        assertEquals(1, appender.list.size(),
                "recoverer must emit exactly one log line per failure, got: " + appender.list);
        return new Outcome(captor.getValue(), appender.list.get(0));
    }

    @Test
    void poisonException_isNotLoggedAsARetry() {
        Outcome outcome = run(retryConfig(3, false), record(), new ValidationException("Query Type is null."));

        assertEquals(ERROR_TOPIC, outcome.topic(), "precondition: poison must route to -Error");
        assertFalse(outcome.message().contains("Retry attempt"),
                "record was dead-lettered but logged as a retry: " + outcome.message());
        assertEquals(Level.WARN, outcome.log().getLevel(),
                "dead-lettering must be logged at WARN, not buried at INFO: " + outcome.message());
    }

    @Test
    void disabledRetryConsumer_isNotLoggedAsARetry() {
        Outcome outcome = run(retryConfig(3, true), record(), new RuntimeException("transient boom"));

        assertEquals(ERROR_TOPIC, outcome.topic(), "precondition: disabled retries must route to -Error");
        assertFalse(outcome.message().contains("Retry attempt"),
                "record was dead-lettered but logged as a retry: " + outcome.message());
        assertEquals(Level.WARN, outcome.log().getLevel(),
                "dead-lettering must be logged at WARN, not buried at INFO: " + outcome.message());
    }

    @Test
    void exhaustedAttempts_isLoggedAsADeadLetter() {
        ConsumerRecord<String, String> record = record();
        stampAttempts(record, 2);

        Outcome outcome = run(retryConfig(3, false), record, new RuntimeException("still failing"));

        assertEquals(ERROR_TOPIC, outcome.topic(), "precondition: exhausted attempts must route to -Error");
        assertFalse(outcome.message().contains("Retry attempt"),
                "record was dead-lettered but logged as a retry: " + outcome.message());
        assertEquals(Level.WARN, outcome.log().getLevel(),
                "dead-lettering must be logged at WARN: " + outcome.message());
    }

    @Test
    void transientFailure_withAttemptsRemaining_isLoggedAsARetry() {
        Outcome outcome = run(retryConfig(3, false), record(), new RuntimeException("transient boom"));

        assertEquals(RETRY_TOPIC, outcome.topic(), "precondition: transient failure must route to -Retry");
        assertTrue(outcome.message().contains("Retry attempt"),
                "record was retried but not logged as a retry: " + outcome.message());
    }

    // ---- Backoff-timestamp header. It is a due-time: "do not deliver before this instant", read by the
    //      backoff-aware -Retry listener. A record going to -Error will never be delivered again, so a
    //      due-time on it describes a retry that was never scheduled. Nothing reads it there today, which
    //      is exactly why it is worth pinning: dead-letter replay tooling would find headers implying a
    //      pending retry on records that are finished.

    @Test
    void retriedRecord_carriesBackoffDueTime() {
        Outcome outcome = run(retryConfig(3, false), record(), new RuntimeException("transient boom"));

        assertEquals(RETRY_TOPIC, outcome.topic(), "precondition: transient failure must route to -Retry");
        Header backoff = outcome.header(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP);
        assertNotNull(backoff, "a record that will be retried must carry the due-time the listener waits on");
        assertTrue(new BigInteger(backoff.value()).longValue() >= System.currentTimeMillis() - 1_000L,
                "due-time must be in the future, not a stale instant");
    }

    @Test
    void poisonRecord_carriesNoBackoffDueTime() {
        Outcome outcome = run(retryConfig(3, false), record(), new ValidationException("Query Type is null."));

        assertEquals(ERROR_TOPIC, outcome.topic(), "precondition: poison must route to -Error");
        assertNull(outcome.header(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP),
                "a dead-lettered record must not advertise a due-time for a retry that will never happen");
    }

    @Test
    void exhaustedRecord_carriesNoBackoffDueTime() {
        ConsumerRecord<String, String> record = record();
        stampAttempts(record, 2);

        Outcome outcome = run(retryConfig(3, false), record, new RuntimeException("still failing"));

        assertEquals(ERROR_TOPIC, outcome.topic(), "precondition: exhausted attempts must route to -Error");
        assertNull(outcome.header(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP),
                "a dead-lettered record must not advertise a due-time for a retry that will never happen");
    }

    @Test
    void exhaustedRecord_dropsTheDueTimeLeftByItsPreviousRetry() {
        // The realistic path: this record retried before, so it arrives carrying the due-time stamped on
        // the last hop. Not stamping a new one is not enough — the stale one must be cleared.
        ConsumerRecord<String, String> record = record();
        stampAttempts(record, 2);
        record.headers().add(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP,
                BigInteger.valueOf(System.currentTimeMillis() + 60_000L).toByteArray());

        Outcome outcome = run(retryConfig(3, false), record, new RuntimeException("still failing"));

        assertEquals(ERROR_TOPIC, outcome.topic(), "precondition: exhausted attempts must route to -Error");
        assertNull(outcome.header(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP),
                "the due-time from the previous retry must be removed, not carried onto the dead letter");
    }

    @Test
    void deadLetteredRecord_stillRecordsHowManyAttemptsItGot() {
        // attempts is history, not a promise — it stays, so a dead letter says how many times it ran.
        Outcome outcome = run(retryConfig(3, false), record(), new ValidationException("Query Type is null."));

        Header attempts = outcome.header(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS);
        assertNotNull(attempts, "attempts must survive onto the dead letter");
        assertEquals(1, ByteBuffer.wrap(attempts.value()).getInt(),
                "poison failed once and was never retried, so it must record exactly one attempt");
    }
}
