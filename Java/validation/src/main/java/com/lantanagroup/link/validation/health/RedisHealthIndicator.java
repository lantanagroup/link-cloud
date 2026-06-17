package com.lantanagroup.link.validation.health;

import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.data.redis.connection.RedisConnection;
import org.springframework.data.redis.connection.RedisConnectionFactory;
import org.springframework.stereotype.Component;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.SynchronousQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

/**
 * Reports Redis cache reachability under the "redis" component so the Admin BFF can map it to the
 * dashboard's Cache column. Registered only when {@code cache.type=redis} (the same condition that
 * creates the Redis connection factory). Spring's auto-configured Redis health indicator is disabled
 * (management.health.redis.enabled=false) in favor of this one: when Redis is unreachable, Lettuce's
 * reconnect / connection-acquisition path is not reliably bounded by the command timeout, so the
 * default indicator can leave /health hanging long enough for the BFF's 5s window to time out and
 * report every column as N/A.
 * <p>
 * The PING runs on a separate thread bounded by {@link #checkTimeoutMs}, so a down cache is reported
 * DOWN within the deadline.
 */
@Component("redis")
@ConditionalOnProperty(name = "cache.type", havingValue = "redis")
public class RedisHealthIndicator implements HealthIndicator {

    private static final Logger logger = LoggerFactory.getLogger(RedisHealthIndicator.class);

    private final RedisConnectionFactory connectionFactory;
    // Backstop deadline for the PING; configurable via management.health.redis.timeout-ms (default 3s).
    private final long checkTimeoutMs;
    // Single bounded worker with no queue (SynchronousQueue): while a PING is still running -- e.g.
    // stuck on a non-interruptible Lettuce call -- further health requests are rejected rather than
    // spawning unbounded daemon threads. A rejection surfaces as DOWN via the catch below.
    // Daemon thread so a stuck PING never blocks JVM shutdown.
    private final ExecutorService executor = new ThreadPoolExecutor(
            1, 1, 0L, TimeUnit.MILLISECONDS,
            new SynchronousQueue<>(),
            runnable -> {
                Thread t = new Thread(runnable, "redis-health-check");
                t.setDaemon(true);
                return t;
            });

    public RedisHealthIndicator(
            RedisConnectionFactory connectionFactory,
            @Value("${management.health.redis.timeout-ms:3000}") long checkTimeoutMs) {
        this.connectionFactory = connectionFactory;
        this.checkTimeoutMs = checkTimeoutMs;
    }

    @Override
    public Health health() {
        if (checkRedisConnection()) {
            return Health.up().withDetail("Cache", "Available").build();
        } else {
            return Health.down().withDetail("Cache", "Unavailable").build();
        }
    }

    private boolean checkRedisConnection() {
        CompletableFuture<String> ping = null;
        try {
            ping = CompletableFuture.supplyAsync(() -> {
                try (RedisConnection connection = connectionFactory.getConnection()) {
                    return connection.ping();
                }
            }, executor);
            // bound the check so an unreachable Redis is reported DOWN within the deadline rather
            // than waiting on Lettuce's reconnect/connect behavior
            String response = ping.get(checkTimeoutMs, TimeUnit.MILLISECONDS);
            return "PONG".equalsIgnoreCase(response);
        } catch (Exception e) {
            // includes RejectedExecutionException when a previous probe is still occupying the worker
            if (ping != null) {
                ping.cancel(true);
            }
            logger.warn("Redis health check failed: {}", LogUtils.sanitize(e.getMessage()));
            return false;
        }
    }
}
