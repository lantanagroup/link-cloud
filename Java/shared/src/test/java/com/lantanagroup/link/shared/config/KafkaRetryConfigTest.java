package com.lantanagroup.link.shared.config;

import org.junit.jupiter.api.Test;

import java.time.Duration;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;

/**
 * {@link KafkaRetryConfig} is array-driven only: {@code consumer-retry-duration} is the single source of
 * truth for retry depth and timing. These verify the derived attempt count and the per-attempt backoff
 * (including clamping and the empty-list "no retries" case).
 */
class KafkaRetryConfigTest {

    @Test
    void defaultSchedule_isThreeAttemptsThreeSecondsApart() {
        KafkaRetryConfig config = new KafkaRetryConfig();

        assertEquals(3, config.effectiveMaxAttempts());
        assertEquals(3_000L, config.backoffMsForAttempt(1));
        assertEquals(3_000L, config.backoffMsForAttempt(2));
    }

    @Test
    void attemptCount_isDerivedFromArraySize() {
        KafkaRetryConfig config = new KafkaRetryConfig();
        config.setConsumerRetryDuration(List.of(
                Duration.ofSeconds(20), Duration.ofSeconds(60), Duration.ofSeconds(120)));

        // size + 1: the original attempt plus one retry per configured delay.
        assertEquals(4, config.effectiveMaxAttempts());
    }

    @Test
    void perAttemptBackoff_followsTheArray_andClampsAtBothEnds() {
        KafkaRetryConfig config = new KafkaRetryConfig();
        config.setConsumerRetryDuration(List.of(
                Duration.ofSeconds(20), Duration.ofSeconds(60), Duration.ofSeconds(120)));

        assertEquals(20_000L, config.backoffMsForAttempt(0));   // clamp low
        assertEquals(20_000L, config.backoffMsForAttempt(1));
        assertEquals(60_000L, config.backoffMsForAttempt(2));
        assertEquals(120_000L, config.backoffMsForAttempt(3));
        assertEquals(120_000L, config.backoffMsForAttempt(9));  // clamp high
    }

    @Test
    void emptyArray_disablesRetries_andHasNoBackoff() {
        KafkaRetryConfig config = new KafkaRetryConfig();
        config.setConsumerRetryDuration(List.of());

        assertEquals(1, config.effectiveMaxAttempts());   // first failure dead-lettered, no retry
        assertEquals(0L, config.backoffMsForAttempt(1));  // never indexes an empty list
    }

    @Test
    void disableRetryConsumer_defaultsFalse_andIsSettable() {
        KafkaRetryConfig config = new KafkaRetryConfig();

        assertFalse(config.isDisableRetryConsumer());
        config.setDisableRetryConsumer(true);
        assertTrue(config.isDisableRetryConsumer());
    }
}
