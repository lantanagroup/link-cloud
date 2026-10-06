package com.lantanagroup.link.measureeval.health;

import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.data.mongodb.core.MongoTemplate;
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
 * Mongo health for MeasureEval.
 * <p>
 * The ping runs on a separate thread bounded by {@link #checkTimeoutMs} rather than inline, because
 * an unreachable mongod leaves the driver in server selection for its full {@code
 * serverSelectionTimeoutMS} -- 30s on the driver default, and the URI is supplied per environment
 * (Azure App Configuration in deployed environments), so the parameter cannot be relied on. Thirty
 * seconds pushes /health past the BFF's 5s health-check timeout, and the BFF then reports the whole
 * service Unhealthy with no entries at all -- so the Admin dashboard loses the MongoDB *and* the
 * Resource Cache rows precisely when they would say what is wrong. The backstop is kept below that
 * 5s deadline, with room for the resource-cache check to spend its own budget, since Spring runs
 * indicators sequentially on the request thread.
 * <p>
 * Spring's auto Mongo indicator is disabled (management.health.mongo.enabled=false) in favor of this
 * one.
 *
 * @see ResourceCacheHealthIndicator a bounded probe on the same reasoning, for Redis + ABS
 */
@Component("MongoDB")
public class MongoHealthIndicator implements HealthIndicator {

    private static final Logger logger = LoggerFactory.getLogger(MongoHealthIndicator.class);
    private static final String AVAILABLE = "Available";
    private static final String UNAVAILABLE = "Unavailable";

    private final MongoTemplate mongoTemplate;
    // Backstop deadline for the ping; configurable via management.health.mongo.timeout-ms.
    private final long checkTimeoutMs;
    // Single bounded worker with no queue (SynchronousQueue): at most one ping reaches Mongo at a
    // time, however many /health requests arrive. Daemon thread so a stuck ping never blocks JVM
    // shutdown.
    private final ExecutorService executor = new ThreadPoolExecutor(
            1, 1, 0L, TimeUnit.MILLISECONDS,
            new SynchronousQueue<>(),
            runnable -> {
                Thread t = new Thread(runnable, "mongo-health-check");
                t.setDaemon(true);
                return t;
            });
    // Concurrent callers share the ping that is already running rather than being turned away. The
    // container healthcheck polls /health every few seconds, so the BFF's request routinely lands
    // while a ping is in flight, and rejecting it there reported a reason that described the executor
    // rather than Mongo. Sharing also means a ping is never cancelled, so the worker is released
    // exactly when the driver call returns and the next request can start a fresh ping.
    private final AtomicReference<CompletableFuture<Void>> inFlight = new AtomicReference<>();

    public MongoHealthIndicator(
            MongoTemplate mongoTemplate,
            @Value("${management.health.mongo.timeout-ms:2000}") long checkTimeoutMs) {
        this.mongoTemplate = mongoTemplate;
        this.checkTimeoutMs = checkTimeoutMs;
    }

    @Override
    public Health health() {
        try {
            currentPing().get(checkTimeoutMs, TimeUnit.MILLISECONDS);
            return Health.up().withDetail("Database", AVAILABLE).build();
        } catch (TimeoutException e) {
            // Deliberately not cancelled: the ping stays shared so later callers get this same
            // verdict until the driver call returns, instead of being rejected with a bogus reason.
            logger.warn("Mongo health check timed out after {}ms", checkTimeoutMs);
            return down("Mongo health check timed out after " + checkTimeoutMs + "ms");
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            logger.warn("Mongo health check interrupted");
            return down("Mongo health check interrupted");
        } catch (ExecutionException e) {
            // The ping itself threw; unwrap to the underlying cause for an accurate reason.
            Throwable cause = e.getCause() != null ? e.getCause() : e;
            String reason = cause instanceof RejectedExecutionException
                    // A previous ping's thread is still stuck in a call that ignores interruption.
                    ? "Mongo health check could not be scheduled"
                    : LogUtils.sanitize(cause.getMessage());
            // Log it in full; the detail carries only as much as a dashboard cell can show.
            logger.warn("Mongo health check failed: {}", reason);
            return down(shorten(reason));
        }
    }

    /**
     * Returns the ping already running, or starts one. Never returns null and never throws: a worker
     * that cannot be scheduled is reported through the returned future, so every caller leaves
     * {@link #health()} by the same path.
     */
    private CompletableFuture<Void> currentPing() {
        while (true) {
            CompletableFuture<Void> running = inFlight.get();
            if (running != null && !running.isDone()) {
                return running;
            }
            CompletableFuture<Void> started = new CompletableFuture<>();
            if (inFlight.compareAndSet(running, started)) {
                try {
                    executor.execute(() -> {
                        try {
                            mongoTemplate.executeCommand("{ ping: 1 }");
                            started.complete(null);
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

    /**
     * The Mongo driver reports an unreachable cluster by dumping its whole client-side view of every
     * server, which runs past a thousand characters. The detail lands in a table cell on the Admin
     * dashboard, so keep the leading sentence -- which carries the actual reason -- and drop the rest;
     * the full message is in the log line above.
     */
    private static String shorten(String reason) {
        int limit = 200;
        return reason == null || reason.length() <= limit ? reason : reason.substring(0, limit) + "...";
    }

    /**
     * The Database detail is reported on every failure, not just the reachable-but-erroring one, so
     * the Admin dashboard's MongoDB row keeps rendering whatever went wrong.
     */
    private Health down(String reason) {
        return Health.down()
                .withDetail("Database", UNAVAILABLE)
                .withDetail("error", reason)
                .build();
    }
}
