package com.lantanagroup.link.validation.health;

import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.stereotype.Component;

import javax.sql.DataSource;
import java.sql.Connection;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.SynchronousQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

/**
 * Reports database reachability, bounded by {@link #checkTimeoutMs}.
 * <p>
 * Spring's auto-configured indicator is disabled (management.health.db.enabled=false) in favour of
 * this one. That one asks the pool for a connection with nothing but the pool's own limit behind it,
 * and HikariCP's default {@code connectionTimeout} is 30s -- six times the Admin BFF's health-check
 * window. A single unreachable database therefore kept /health from answering at all, and the BFF
 * reported the service Unhealthy with no entries, so the dashboard lost the Redis row as well and
 * said nothing about why. Bounding it here rather than through {@code hikari.connection-timeout}
 * keeps real query traffic on the pool's own settings: a health check and a user's request want
 * different patience.
 *
 * @see RedisHealthIndicator a bounded probe on the same reasoning, for Redis
 */
@Component("db")
public class DatabaseHealthIndicator implements HealthIndicator {

    private static final Logger logger = LoggerFactory.getLogger(DatabaseHealthIndicator.class);
    private static final String AVAILABLE = "Available";
    private static final String UNAVAILABLE = "Unavailable";

    /** Seconds given to the driver's own validation round-trip, inside the deadline below. */
    private static final int VALIDATION_TIMEOUT_SECONDS = 1;

    private final DataSource dataSource;
    // Backstop deadline for the probe; configurable via management.health.db.timeout-ms.
    private final long checkTimeoutMs;
    // Single bounded worker with no queue (SynchronousQueue): while a probe is still running -- e.g.
    // stuck waiting on the pool -- further health requests are rejected rather than spawning
    // unbounded threads, and a rejection surfaces as DOWN through the catch below. Daemon thread so
    // a stuck probe never blocks JVM shutdown.
    private final ExecutorService executor = new ThreadPoolExecutor(
            1, 1, 0L, TimeUnit.MILLISECONDS,
            new SynchronousQueue<>(),
            runnable -> {
                Thread t = new Thread(runnable, "db-health-check");
                t.setDaemon(true);
                return t;
            });

    public DatabaseHealthIndicator(
            DataSource dataSource,
            @Value("${management.health.db.timeout-ms:2000}") long checkTimeoutMs) {
        this.dataSource = dataSource;
        this.checkTimeoutMs = checkTimeoutMs;
    }

    @Override
    public Health health() {
        return checkConnection()
                ? Health.up().withDetail("Database", AVAILABLE).build()
                : Health.down().withDetail("Database", UNAVAILABLE).build();
    }

    private boolean checkConnection() {
        CompletableFuture<Boolean> probe = null;
        try {
            probe = CompletableFuture.supplyAsync(() -> {
                try (Connection connection = dataSource.getConnection()) {
                    return connection.isValid(VALIDATION_TIMEOUT_SECONDS);
                } catch (Exception e) {
                    // Wrapped rather than logged here: the reason is logged once, below, where the
                    // rejected and timed-out cases are handled on the same path.
                    throw new IllegalStateException(e);
                }
            }, executor);

            return Boolean.TRUE.equals(probe.get(checkTimeoutMs, TimeUnit.MILLISECONDS));
        } catch (Exception e) {
            // Includes the timeout, and RejectedExecutionException when a previous probe still
            // occupies the worker -- that one is thrown by supplyAsync itself, before the assignment
            // above, so there is nothing to cancel.
            if (probe != null) {
                probe.cancel(true);
            }

            // The message is logged and never returned. GET /health is permitAll in both
            // authentication modes (SecurityHelper), so a detail is readable by anyone who can reach
            // the port, and a JDBC failure names the host, instance and sometimes the login.
            logger.warn("Database health check failed: {}", LogUtils.sanitize(e.getMessage()));

            return false;
        }
    }
}
