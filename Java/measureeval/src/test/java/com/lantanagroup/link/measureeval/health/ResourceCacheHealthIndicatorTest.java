package com.lantanagroup.link.measureeval.health;

import com.lantanagroup.link.measureeval.services.AbsResourceService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.Status;
import org.springframework.data.redis.connection.RedisConnection;
import org.springframework.data.redis.connection.RedisConnectionFactory;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

/**
 * The verdict is asymmetric on purpose: ABS is the durable source, Redis only a cache in front of it.
 * Losing Redis makes every read slower, not broken, so it must not read as Unhealthy -- containers
 * would be restarted over something the design absorbs -- but it must not read as fully Healthy
 * either, which is what hid Redis outages behind a green check.
 */
public class ResourceCacheHealthIndicatorTest {

    private static final long TIMEOUT_MS = 3000;

    private RedisConnectionFactory redisConnectionFactory;
    private RedisConnection redisConnection;
    private AbsResourceService absResourceService;
    private ResourceCacheHealthIndicator indicator;

    @SuppressWarnings("unchecked")
    @BeforeEach
    void setUp() {
        redisConnection = mock(RedisConnection.class);
        redisConnectionFactory = mock(RedisConnectionFactory.class);
        when(redisConnectionFactory.getConnection()).thenReturn(redisConnection);

        absResourceService = mock(AbsResourceService.class);
        ObjectProvider<AbsResourceService> provider = mock(ObjectProvider.class);
        when(provider.getIfAvailable()).thenReturn(absResourceService);

        indicator = new ResourceCacheHealthIndicator(redisConnectionFactory, provider, TIMEOUT_MS);
    }

    @Test
    void whenBothStoresAreReachable_thenHealthIsUp() {
        when(redisConnection.ping()).thenReturn("PONG");
        when(absResourceService.isContainerAvailable()).thenReturn(true);

        Health health = indicator.health();

        assertEquals(Status.UP, health.getStatus());
        assertEquals("Available", health.getDetails().get("Redis"));
        assertEquals("Available", health.getDetails().get("ABS"));
    }

    @Test
    void whenOnlyRedisIsUnreachable_thenHealthIsDegraded() {
        when(redisConnection.ping()).thenThrow(new RuntimeException("Redis command timed out"));
        when(absResourceService.isContainerAvailable()).thenReturn(true);

        Health health = indicator.health();

        // Not DOWN: reads still succeed from ABS. Not UP either: the dashboard has to show it.
        assertEquals("DEGRADED", health.getStatus().getCode());
        assertEquals("Unavailable", health.getDetails().get("Redis"));
        assertEquals("Available", health.getDetails().get("ABS"));
    }

    @Test
    void whenAbsIsUnreachable_thenHealthIsDown() {
        when(redisConnection.ping()).thenReturn("PONG");
        when(absResourceService.isContainerAvailable()).thenReturn(false);

        Health health = indicator.health();

        assertEquals(Status.DOWN, health.getStatus());
        assertEquals("Available", health.getDetails().get("Redis"));
        assertEquals("Unavailable", health.getDetails().get("ABS"));
    }

    @Test
    void whenBothStoresAreUnreachable_thenHealthIsDown() {
        when(redisConnection.ping()).thenThrow(new RuntimeException("Redis command timed out"));
        when(absResourceService.isContainerAvailable()).thenThrow(new RuntimeException("ABS unreachable"));

        Health health = indicator.health();

        // ABS decides the verdict, so a Redis outage never softens it to DEGRADED.
        assertEquals(Status.DOWN, health.getStatus());
        assertEquals("Unavailable", health.getDetails().get("Redis"));
        assertEquals("Unavailable", health.getDetails().get("ABS"));
    }

    /**
     * The container healthcheck polls /health every few seconds, so the BFF's request routinely lands
     * while a probe is still in flight. Turning that caller away reported a Redis-only outage as DOWN
     * -- a restart-worthy verdict for something the design absorbs -- so concurrent callers share the
     * running probe and both see DEGRADED.
     */
    @Test
    void whenAProbeIsAlreadyRunning_thenConcurrentCallersShareItAndStillSeeDegraded() throws Exception {
        CountDownLatch probeStarted = new CountDownLatch(1);
        CountDownLatch release = new CountDownLatch(1);
        when(redisConnection.ping()).thenAnswer(invocation -> {
            probeStarted.countDown();
            release.await(10, TimeUnit.SECONDS);
            throw new RuntimeException("Redis command timed out");
        });
        when(absResourceService.isContainerAvailable()).thenReturn(true);

        ExecutorService callers = Executors.newFixedThreadPool(2);
        try {
            Future<Health> first = callers.submit(indicator::health);
            assertTrue(probeStarted.await(10, TimeUnit.SECONDS), "the first probe never started");
            Future<Health> second = callers.submit(indicator::health);
            // Give the second caller time to join the running probe before it completes.
            Thread.sleep(100);
            release.countDown();

            assertEquals("DEGRADED", first.get(10, TimeUnit.SECONDS).getStatus().getCode());
            assertEquals("DEGRADED", second.get(10, TimeUnit.SECONDS).getStatus().getCode());
            // One probe served both callers, so Redis was only contacted once.
            verify(redisConnectionFactory, times(1)).getConnection();
        } finally {
            callers.shutdownNow();
        }
    }

    @SuppressWarnings("unchecked")
    @Test
    void whenAbsIsNotConfigured_thenHealthIsDown() {
        when(redisConnection.ping()).thenReturn("PONG");
        ObjectProvider<AbsResourceService> empty = mock(ObjectProvider.class);
        when(empty.getIfAvailable()).thenReturn(null);

        Health health = new ResourceCacheHealthIndicator(redisConnectionFactory, empty, TIMEOUT_MS).health();

        assertEquals(Status.DOWN, health.getStatus());
        assertEquals("Not configured", health.getDetails().get("ABS"));
    }
}
