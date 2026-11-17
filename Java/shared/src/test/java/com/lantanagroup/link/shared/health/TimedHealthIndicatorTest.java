package com.lantanagroup.link.shared.health;

import org.junit.jupiter.api.Test;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.Status;

import java.util.function.Supplier;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertTrue;

class TimedHealthIndicatorTest {

    /**
     * The dashboard reads a row's status from the first and its explanation from the second, so
     * wrapping the probe must not cost either.
     */
    @Test
    void theProbesStatusAndDetailsSurviveTheTiming() {
        Health health = probe(() -> Health.down()
                .withDetail("Redis", "Available")
                .withDetail("ABS", "Unavailable")
                .build()).health();

        assertEquals(Status.DOWN, health.getStatus());
        assertEquals("Available", health.getDetails().get("Redis"));
        assertEquals("Unavailable", health.getDetails().get("ABS"));
    }

    @Test
    void theElapsedTimeIsReported() {
        Health health = probe(() -> {
            try {
                Thread.sleep(50);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }
            return Health.up().build();
        }).health();

        Object reported = health.getDetails().get(TimedHealthIndicator.DURATION_DETAIL);

        // A number rather than a formatted string: the Admin BFF reads it as one.
        Long elapsedMs = assertInstanceOf(Long.class, reported);
        assertTrue(elapsedMs >= 50, "a probe that slept 50ms cannot report " + elapsedMs + "ms");
    }

    private static TimedHealthIndicator probe(Supplier<Health> check) {
        return new TimedHealthIndicator() {
            @Override
            protected Health check() {
                return check.get();
            }
        };
    }
}
