package com.lantanagroup.link.shared.health;

import com.lantanagroup.link.shared.kafka.Properties;
import org.junit.jupiter.api.Test;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.Status;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.support.SendResult;

import java.util.concurrent.CompletableFuture;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * The Admin BFF abandons a service's {@code /health} at its own deadline and reports it Unhealthy
 * with no entries at all, so every indicator's budget is spent out of one shared window. These
 * cover the Kafka indicator's share of it.
 */
class KafkaHealthCheckIndicatorTest {

    @SuppressWarnings("unchecked")
    private final KafkaTemplate<String, String> kafkaTemplate = mock(KafkaTemplate.class);

    @Test
    void whenTheBrokerAcknowledges_thenUp() {
        CompletableFuture<SendResult<String, String>> acknowledged = new CompletableFuture<>();
        acknowledged.complete(null);
        when(kafkaTemplate.send(anyString(), anyString(), anyString())).thenReturn(acknowledged);

        Health health = new KafkaHealthCheckIndicator(kafkaTemplate, "test", 1500).health();

        assertEquals(Status.UP, health.getStatus());
        assertEquals("Available", health.getDetails().get("Kafka"));
    }

    /**
     * An unreachable broker is the case that used to dominate the window: the producer blocked for
     * max.block.ms and the get waited a further second on top, so the check alone could spend 4s of
     * the 5s and the Mongo and Resource Cache rows vanished with it.
     */
    @Test
    void whenTheBrokerNeverAcknowledges_thenDownAtItsOwnDeadline() {
        // A send that is never acknowledged, the way it behaves with no broker reachable.
        when(kafkaTemplate.send(anyString(), anyString(), anyString()))
                .thenReturn(new CompletableFuture<>());

        long startedAt = System.nanoTime();
        Health health = new KafkaHealthCheckIndicator(kafkaTemplate, "test", 200).health();
        long elapsedMs = (System.nanoTime() - startedAt) / 1_000_000;

        assertEquals(Status.DOWN, health.getStatus());
        assertEquals("Unavailable", health.getDetails().get("Kafka"));
        assertTrue(elapsedMs < 2000,
                "the check must return at its own deadline, not the producer's; took " + elapsedMs + "ms");
    }

    /**
     * Guards the two halves of the budget against drifting apart: the producer has to give up first,
     * so that a broker outage is reported as one rather than as a timed-out future, and the pair has
     * to leave the other indicators room inside the BFF's health-check window.
     */
    @Test
    void theProducerGivesUpBeforeTheIndicatorAndBothFitTheBffWindow() {
        long defaultTimeoutMs = Long.parseLong(KafkaHealthCheckIndicator.DEFAULT_TIMEOUT_MS);

        assertTrue(Properties.MAX_BLOCK_MS_CONFIG < defaultTimeoutMs,
                "the health producer's max.block.ms (" + Properties.MAX_BLOCK_MS_CONFIG
                        + "ms) must stay under the indicator's deadline (" + defaultTimeoutMs + "ms)");
        assertTrue(defaultTimeoutMs <= 2000,
                "the Kafka check shares the BFF's health-check window with the other indicators; "
                        + defaultTimeoutMs + "ms leaves too little for them");
    }
}
