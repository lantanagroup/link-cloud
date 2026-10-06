package com.lantanagroup.link.measureeval.health;

import com.lantanagroup.link.measureeval.services.AbsResourceService;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.boot.actuate.health.Status;
import org.springframework.data.redis.connection.RedisConnection;
import org.springframework.data.redis.connection.RedisConnectionFactory;
import org.springframework.stereotype.Component;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.SynchronousQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Unified resource-cache health for MeasureEval, mirroring the .NET {@code ResourceCacheHealthCheck}.
 * Since LEGLINK-1279 the stores are asymmetric: ABS is the durable source (reads fall back to it, so
 * ABS unreachable means resources may be unreadable -- DOWN), while Redis is only a cache in front of
 * it (Redis unreachable degrades reads to ABS speed but loses nothing -- DEGRADED, not DOWN, so the
 * Admin dashboard shows it in amber without the service being restarted over it). Both statuses are
 * always reported as details under the "Resource Cache" component.
 * <p>
 * Spring's auto Redis indicator is disabled (management.health.redis.enabled=false) in favor of this
 * one.
 * <p>
 * The combined probe runs on a separate thread bounded by {@link #checkTimeoutMs}, because both the
 * Lettuce reconnect path and the Azure SDK's retry/backoff can otherwise leave /health hanging when a
 * backend is unreachable. The backstop is kept below the BFF's 5s health-check timeout so the BFF
 * still receives a real verdict rather than timing out to N/A.
 */
@Component("Resource Cache")
public class ResourceCacheHealthIndicator implements HealthIndicator {

    private static final Logger logger = LoggerFactory.getLogger(ResourceCacheHealthIndicator.class);
    private static final String AVAILABLE = "Available";
    private static final String UNAVAILABLE = "Unavailable";
    private static final String NOT_CONFIGURED = "Not configured";
    // Custom Spring status, mirroring the .NET ResourceCacheHealthCheck's HealthCheckResult.Degraded.
    // management.endpoint.health.status.order ranks it between DOWN and UP, and it is mapped to HTTP
    // 200 so neither the container healthcheck nor Kubernetes restarts the pod over a Redis outage.
    private static final Status DEGRADED = new Status(
            "DEGRADED", "Resource cache is serving reads from blob storage; Redis is unavailable.");

    private final RedisConnectionFactory redisConnectionFactory;
    private final ObjectProvider<AbsResourceService> absResourceServiceProvider;
    // Backstop deadline for the combined check; configurable via management.health.resource-cache.timeout-ms.
    private final long checkTimeoutMs;
    // Single bounded worker with no queue (SynchronousQueue): at most one probe touches the backends
    // at a time, however many /health requests arrive. Daemon thread so a stuck probe never blocks
    // JVM shutdown.
    private final ExecutorService executor = new ThreadPoolExecutor(
            1, 1, 0L, TimeUnit.MILLISECONDS,
            new SynchronousQueue<>(),
            runnable -> {
                Thread t = new Thread(runnable, "resource-cache-health-check");
                t.setDaemon(true);
                return t;
            });
    // Concurrent callers share the probe that is already running rather than being turned away. The
    // container healthcheck polls /health every few seconds, so the BFF's request routinely lands
    // while a probe is in flight; rejecting it there reported a Redis-only outage as DOWN instead of
    // DEGRADED. Sharing also means a probe is never cancelled, so the worker is released exactly when
    // the backend call returns and the next request can start a fresh probe.
    private final AtomicReference<CompletableFuture<CacheStatus>> inFlight = new AtomicReference<>();

    public ResourceCacheHealthIndicator(
            RedisConnectionFactory redisConnectionFactory,
            ObjectProvider<AbsResourceService> absResourceServiceProvider,
            @Value("${management.health.resource-cache.timeout-ms:3000}") long checkTimeoutMs) {
        this.redisConnectionFactory = redisConnectionFactory;
        this.absResourceServiceProvider = absResourceServiceProvider;
        this.checkTimeoutMs = checkTimeoutMs;
    }

    @Override
    public Health health() {
        try {
            CacheStatus status = currentProbe().get(checkTimeoutMs, TimeUnit.MILLISECONDS);
            // ABS is the durable source: unreachable (or missing) ABS is DOWN, whatever Redis says.
            // Redis is only the cache in front of it, so a Redis outage is DEGRADED rather than DOWN.
            Health.Builder builder;
            if (!AVAILABLE.equals(status.abs)) {
                builder = Health.down();
            } else if (!AVAILABLE.equals(status.redis)) {
                logger.warn("Redis resource cache unavailable; reads are degraded to ABS until it recovers");
                builder = Health.status(DEGRADED);
            } else {
                builder = Health.up();
            }
            return builder.withDetail("Redis", status.redis).withDetail("ABS", status.abs).build();
        } catch (TimeoutException e) {
            // Deliberately not cancelled: the probe stays shared so later callers get this same
            // verdict until the backend call returns, instead of being rejected with a bogus reason.
            logger.warn("Resource cache health check timed out after {}ms", checkTimeoutMs);
            return Health.down()
                    .withDetail("error", "Resource cache health check timed out after " + checkTimeoutMs + "ms")
                    .build();
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            logger.warn("Resource cache health check interrupted");
            return Health.down().withDetail("error", "Resource cache health check interrupted").build();
        } catch (ExecutionException e) {
            // The probe itself threw; unwrap to the underlying cause for an accurate reason.
            Throwable cause = e.getCause() != null ? e.getCause() : e;
            String reason = cause instanceof RejectedExecutionException
                    // A previous probe's thread is still stuck in a call that ignores interruption.
                    ? "Resource cache health check could not be scheduled"
                    : LogUtils.sanitize(cause.getMessage());
            logger.warn("Resource cache health check failed: {}", reason);
            return Health.down().withDetail("error", reason).build();
        }
    }

    /**
     * Returns the probe already running, or starts one. Never returns null and never throws: a worker
     * that cannot be scheduled is reported through the returned future, so every caller leaves
     * {@link #health()} by the same path.
     */
    private CompletableFuture<CacheStatus> currentProbe() {
        while (true) {
            CompletableFuture<CacheStatus> running = inFlight.get();
            if (running != null && !running.isDone()) {
                return running;
            }
            CompletableFuture<CacheStatus> started = new CompletableFuture<>();
            if (inFlight.compareAndSet(running, started)) {
                try {
                    executor.execute(() -> {
                        try {
                            started.complete(runChecks());
                        } catch (Throwable t) {
                            started.completeExceptionally(t);
                        }
                    });
                } catch (RejectedExecutionException e) {
                    started.completeExceptionally(e);
                }
                return started;
            }
        }
    }

    private CacheStatus runChecks() {
        return new CacheStatus(checkRedis(), checkAbs());
    }

    private String checkRedis() {
        try (RedisConnection connection = redisConnectionFactory.getConnection()) {
            return "PONG".equalsIgnoreCase(connection.ping()) ? AVAILABLE : UNAVAILABLE;
        } catch (Exception e) {
            logger.warn("Redis cache check failed: {}", LogUtils.sanitize(e.getMessage()));
            return UNAVAILABLE;
        }
    }

    private String checkAbs() {
        AbsResourceService absResourceService = absResourceServiceProvider.getIfAvailable();
        if (absResourceService == null) {
            return NOT_CONFIGURED;
        }
        try {
            return absResourceService.isContainerAvailable() ? AVAILABLE : UNAVAILABLE;
        } catch (Exception e) {
            logger.warn("ABS cache check failed: {}", LogUtils.sanitize(e.getMessage()));
            return UNAVAILABLE;
        }
    }

    private record CacheStatus(String redis, String abs) {
    }
}
