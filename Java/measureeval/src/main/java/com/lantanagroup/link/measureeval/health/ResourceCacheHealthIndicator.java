package com.lantanagroup.link.measureeval.health;

import com.lantanagroup.link.measureeval.services.AbsResourceService;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.data.redis.connection.RedisConnection;
import org.springframework.data.redis.connection.RedisConnectionFactory;
import org.springframework.stereotype.Component;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.SynchronousQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

/**
 * Unified resource-cache health for MeasureEval, mirroring the .NET {@code ResourceCacheHealthCheck}:
 * MeasureEval reads cached resources from either Redis or Azure Blob Storage (ABS) depending on the
 * per-message {@code CacheType}, so this verifies BOTH backends and reports a single combined status
 * under the "resourceCache" component (mapped to the Admin dashboard's Cache column).
 * <p>
 * Redis is always wired; ABS is optional ({@link AbsResourceService} is absent when
 * {@code resource-cache.blob-storage} is not configured), in which case ABS is reported "Not
 * configured" and does not fail the check. Spring's auto Redis indicator is disabled
 * (management.health.redis.enabled=false) in favor of this one.
 * <p>
 * The combined probe runs on a separate thread bounded by {@link #checkTimeoutMs}, because both the
 * Lettuce reconnect path and the Azure SDK's retry/backoff can otherwise leave /health hanging when
 * a backend is unreachable. The backstop is kept below the BFF's 5s health-check timeout so the BFF
 * still receives a real DOWN rather than timing out to N/A.
 */
@Component("resourceCache")
public class ResourceCacheHealthIndicator implements HealthIndicator {

    private static final Logger logger = LoggerFactory.getLogger(ResourceCacheHealthIndicator.class);
    private static final String AVAILABLE = "Available";
    private static final String UNAVAILABLE = "Unavailable";
    private static final String NOT_CONFIGURED = "Not configured";

    private final RedisConnectionFactory redisConnectionFactory;
    private final ObjectProvider<AbsResourceService> absResourceServiceProvider;
    // Backstop deadline for the combined check; configurable via management.health.resource-cache.timeout-ms.
    private final long checkTimeoutMs;
    // Single bounded worker with no queue (SynchronousQueue): while a probe is still running -- e.g.
    // stuck on a non-interruptible Lettuce/Azure call -- further health requests are rejected rather
    // than spawning unbounded daemon threads. A rejection surfaces as DOWN via the catch in health().
    // Daemon thread so a stuck probe never blocks JVM shutdown.
    private final ExecutorService executor = new ThreadPoolExecutor(
            1, 1, 0L, TimeUnit.MILLISECONDS,
            new SynchronousQueue<>(),
            runnable -> {
                Thread t = new Thread(runnable, "resource-cache-health-check");
                t.setDaemon(true);
                return t;
            });

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
        CompletableFuture<CacheStatus> probe = null;
        try {
            probe = CompletableFuture.supplyAsync(this::runChecks, executor);
            CacheStatus status = probe.get(checkTimeoutMs, TimeUnit.MILLISECONDS);
            // Healthy only if Redis is up and ABS is up-or-absent; an unreachable configured backend fails.
            boolean healthy = AVAILABLE.equals(status.redis) && !UNAVAILABLE.equals(status.abs);
            Health.Builder builder = healthy ? Health.up() : Health.down();
            return builder.withDetail("Redis", status.redis).withDetail("ABS", status.abs).build();
        } catch (TimeoutException e) {
            probe.cancel(true);
            logger.warn("Resource cache health check timed out after {}ms", checkTimeoutMs);
            return Health.down().withDetail("error", "Resource cache health check timed out").build();
        } catch (InterruptedException e) {
            probe.cancel(true);
            Thread.currentThread().interrupt();
            logger.warn("Resource cache health check interrupted");
            return Health.down().withDetail("error", "Resource cache health check interrupted").build();
        } catch (ExecutionException e) {
            // The probe itself threw; unwrap to the underlying cause for an accurate reason.
            Throwable cause = e.getCause() != null ? e.getCause() : e;
            String reason = LogUtils.sanitize(cause.getMessage());
            logger.warn("Resource cache health check failed: {}", reason);
            return Health.down().withDetail("error", reason).build();
        } catch (Exception e) {
            // e.g. RejectedExecutionException if the probe could not be scheduled.
            if (probe != null) {
                probe.cancel(true);
            }
            String reason = LogUtils.sanitize(e.getMessage());
            logger.warn("Resource cache health check failed: {}", reason);
            return Health.down().withDetail("error", reason).build();
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
