package com.lantanagroup.link.shared.health;

import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;

/**
 * A health indicator that reports how long its own probe took, as a {@code durationMs} detail.
 * <p>
 * Spring neither times its contributors nor has anywhere to put the figure -- {@link Health} carries
 * a status and details and nothing else -- so the Admin dashboard rendered every Java component at
 * 00:00:00 while the .NET ones showed real numbers. It matters more here than it does there: Spring
 * evaluates indicators sequentially on the request thread, so they spend one shared deadline, and
 * the per-indicator time is what says which backend spent it. Without it the budgets recorded in
 * each service's application.yml can be reasoned about but never observed.
 * <p>
 * Subclasses implement {@link #check()}; the elapsed time is added to the {@link Health} they
 * return. A subclass that throws is reported DOWN by Spring and carries no timing, which is why
 * each of them handles its own failures and returns a Health instead.
 */
public abstract class TimedHealthIndicator implements HealthIndicator {

    /** Detail key the Admin BFF lifts out into the health entry's duration. */
    public static final String DURATION_DETAIL = "durationMs";

    @Override
    public final Health health() {
        long startedAt = System.nanoTime();
        Health health = check();
        long elapsedMs = (System.nanoTime() - startedAt) / 1_000_000;

        // Rebuilt rather than added to: the details map on a built Health is unmodifiable.
        return Health.status(health.getStatus())
                .withDetails(health.getDetails())
                .withDetail(DURATION_DETAIL, elapsedMs)
                .build();
    }

    /**
     * The probe itself. Implementations bound their own work -- the timing here records what was
     * spent, it does not limit it.
     */
    protected abstract Health check();
}
