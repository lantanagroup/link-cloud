package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.shared.kafka.RetryTopicRecoverer;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.common.header.Header;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.apache.kafka.common.record.TimestampType;
import org.junit.jupiter.api.Test;
import org.springframework.kafka.listener.DeadLetterPublishingRecoverer;
import org.springframework.kafka.retrytopic.RetryTopicHeaders;

import java.math.BigInteger;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

/**
 * Encoding-side guard for {@link RetryTopicRecoverer} (async-retry-design.md §6).
 *
 * <p>The danger we are protecting against is a <b>silent</b> break: if our hand-written header bytes
 * stop matching what spring-kafka's backoff-aware listener reads, the code still compiles and routing
 * still "works", but retries no longer honor the backoff. These tests decode the headers with Spring's
 * <i>exact</i> reader ({@code new BigInteger(value).longValue()}) so an encoding drift fails loudly.</p>
 *
 * <p>This covers OUR side of the contract; the behavior side (that the listener actually delays a
 * {@code -Retry} record) is covered by {@code RetryTopicBackoffDelayTest}.</p>
 */
class RetryTopicRecovererTest {

    private static final String TOPIC = "ResourcesNormalized";
    private static final int MAX_ATTEMPTS = 5;
    private static final long BACKOFF_MS = 3_000L;

    private RetryTopicRecoverer newRecoverer(DeadLetterPublishingRecoverer delegate) {
        return new RetryTopicRecoverer(MAX_ATTEMPTS, attempt -> BACKOFF_MS, delegate);
    }

    private ConsumerRecord<String, String> record() {
        return new ConsumerRecord<>(TOPIC, 0, 0L, "key", "value");
    }

    private ConsumerRecord<String, String> recordWithTimestamp(long ts) {
        return new ConsumerRecord<>(TOPIC, 0, 0L, ts, TimestampType.CREATE_TIME,
                0, 0, "key", "value", new RecordHeaders(), Optional.empty());
    }

    /** Decode exactly as spring-kafka's backoff-aware listener does. */
    private static long decodeAsSpring(byte[] value) {
        return new BigInteger(value).longValue();
    }

    private static RecordHeaders attemptsHeader(byte[] value) {
        RecordHeaders headers = new RecordHeaders();
        headers.add(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS, value);
        return headers;
    }

    // ---- Attempts-header tolerance. spring-kafka accepts both the legacy one-byte and the current
    //      four-byte encoding, and records can arrive carrying either (mixed-version rollouts, old
    //      in-flight retry records). A decode failure here would propagate out of the recoverer and
    //      stall the partition (the async consumer skips the ack), so unsupported lengths must read
    //      as 0 - "no attempts yet" - rather than throw.

    @Test
    void currentAttempts_decodesLegacySingleByteHeader() {
        assertEquals(3, RetryTopicRecoverer.currentAttempts(attemptsHeader(new byte[]{3})));
    }

    @Test
    void currentAttempts_returnsZeroForUnsupportedLengths() {
        assertEquals(0, RetryTopicRecoverer.currentAttempts(attemptsHeader(new byte[]{0, 1})),
                "a two-byte value is neither encoding; it must read as zero, not throw");
        assertEquals(0, RetryTopicRecoverer.currentAttempts(attemptsHeader(new byte[]{})),
                "an empty value must read as zero, not throw");
    }

    @Test
    void backoffTimestamp_decodesAsFutureDueTime_viaSpringReader() {
        RetryTopicRecoverer recoverer = newRecoverer(mock(DeadLetterPublishingRecoverer.class));
        ConsumerRecord<String, String> record = record();

        long lowerBound = System.currentTimeMillis() + BACKOFF_MS;
        recoverer.accept(record, new RuntimeException("transient boom"));
        long upperBound = System.currentTimeMillis() + BACKOFF_MS;

        Header header = record.headers().lastHeader(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP);
        assertNotNull(header, "backoff-timestamp header must be stamped");

        long dueAt = decodeAsSpring(header.value());
        assertTrue(dueAt >= lowerBound && dueAt <= upperBound,
                "due-time " + dueAt + " must be now + backoff (" + lowerBound + ".." + upperBound + ")");
    }

    @Test
    void backoffTimestamp_usesMinimalBigIntegerEncoding_notFixedEightBytes() {
        // Regression guard: putLong always writes 8 bytes (zero-padded); BigInteger.toByteArray() writes
        // the minimal two's-complement form. Both happen to decode for positive millis — so this is the
        // ONLY assertion that catches a revert to putLong. If this fails after a refactor, the encoding
        // drifted away from the framework's own representation.
        RetryTopicRecoverer recoverer = newRecoverer(mock(DeadLetterPublishingRecoverer.class));
        ConsumerRecord<String, String> record = record();

        recoverer.accept(record, new RuntimeException("boom"));

        byte[] written = record.headers().lastHeader(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP).value();
        long dueAt = decodeAsSpring(written);
        assertArrayEquals(BigInteger.valueOf(dueAt).toByteArray(), written,
                "backoff timestamp must use BigInteger.toByteArray() (minimal form), not putLong (8 bytes)");
    }

    @Test
    void attempts_incrementsAndRoundTripsViaCurrentAttempts() {
        RetryTopicRecoverer recoverer = newRecoverer(mock(DeadLetterPublishingRecoverer.class));
        ConsumerRecord<String, String> record = record();

        recoverer.accept(record, new RuntimeException("1"));
        assertEquals(1, RetryTopicRecoverer.currentAttempts(record.headers()));

        recoverer.accept(record, new RuntimeException("2"));
        assertEquals(2, RetryTopicRecoverer.currentAttempts(record.headers()));
    }

    @Test
    void backoffTimestamp_usesPerAttemptDelay_fromTable() {
        // Table-driven backoff: delay before attempt 1 = 20s, attempt 2 = 60s, attempt 3 = 120s.
        long[] delaysMs = {20_000L, 60_000L, 120_000L};
        RetryTopicRecoverer recoverer = new RetryTopicRecoverer(
                delaysMs.length + 1,
                attempt -> delaysMs[Math.min(attempt - 1, delaysMs.length - 1)],
                mock(DeadLetterPublishingRecoverer.class));
        ConsumerRecord<String, String> record = record();

        for (int attempt = 1; attempt <= delaysMs.length; attempt++) {
            long lower = System.currentTimeMillis() + delaysMs[attempt - 1];
            recoverer.accept(record, new RuntimeException("attempt " + attempt));
            long upper = System.currentTimeMillis() + delaysMs[attempt - 1];

            long dueAt = decodeAsSpring(
                    record.headers().lastHeader(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP).value());
            assertTrue(dueAt >= lower && dueAt <= upper,
                    "attempt " + attempt + " due-time " + dueAt + " must be now + " + delaysMs[attempt - 1]
                            + "ms (" + lower + ".." + upper + ")");
        }
    }

    @Test
    void originalTimestamp_stampedFromRecordTimestamp_whenAbsent() {
        RetryTopicRecoverer recoverer = newRecoverer(mock(DeadLetterPublishingRecoverer.class));
        long recordTs = 1_700_000_000_000L;
        ConsumerRecord<String, String> record = recordWithTimestamp(recordTs);

        recoverer.accept(record, new RuntimeException("boom"));

        Header header = record.headers().lastHeader(RetryTopicHeaders.DEFAULT_HEADER_ORIGINAL_TIMESTAMP);
        assertNotNull(header, "original-timestamp header must be stamped on first failure");
        assertEquals(recordTs, decodeAsSpring(header.value()));
    }

    @Test
    void originalTimestamp_preservedAcrossRetries() {
        RetryTopicRecoverer recoverer = newRecoverer(mock(DeadLetterPublishingRecoverer.class));
        ConsumerRecord<String, String> record = record();
        // Pre-stamp a sentinel as if a prior attempt already set it; it must NOT be overwritten so total
        // retry age is measured from the first attempt.
        byte[] sentinel = BigInteger.valueOf(12_345L).toByteArray();
        record.headers().add(RetryTopicHeaders.DEFAULT_HEADER_ORIGINAL_TIMESTAMP, sentinel);

        recoverer.accept(record, new RuntimeException("boom"));

        byte[] after = record.headers().lastHeader(RetryTopicHeaders.DEFAULT_HEADER_ORIGINAL_TIMESTAMP).value();
        assertArrayEquals(sentinel, after, "original-timestamp must be preserved, not re-stamped");
    }

    @Test
    void delegate_isInvoked() {
        DeadLetterPublishingRecoverer delegate = mock(DeadLetterPublishingRecoverer.class);
        RetryTopicRecoverer recoverer = newRecoverer(delegate);
        ConsumerRecord<String, String> record = record();
        RuntimeException ex = new RuntimeException("boom");

        recoverer.accept(record, ex);

        verify(delegate, times(1)).accept(record, ex);
    }
}
