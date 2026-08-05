package com.lantanagroup.link.validation.configs;

import lombok.Getter;
import lombok.Setter;
import lombok.extern.slf4j.Slf4j;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.concurrent.ThreadPoolTaskExecutor;

import java.util.concurrent.ThreadPoolExecutor;

/**
 * Configuration for the dedicated thread pool that fans out rubric check execution
 * The checks of a single {@code $evaluate} call are submitted to this
 * pool and merged after all complete.
 *
 * <p>The pool is only used when {@code vaas.checks.parallel} is true, which is <b>not</b> the default.
 * It costs nothing when unused: {@link ThreadPoolTaskExecutor} starts its threads on first submission,
 * so with the flag off no {@code check-exec-} thread is ever created.
 *
 * <p>The pool is intentionally <b>separate</b> from {@link java.util.concurrent.ForkJoinPool#commonPool()},
 * which HAPI's {@code setConcurrentBundleValidation(true)} already uses; sharing it would let the two
 * fight for the same threads. Separate is not independent, though: a {@code FHIR_CONFORMANCE} check
 * running on one of these threads still fans out into the common pool and blocks there, so both pools
 * draw on the same cores.
 *
 * <p><b>Why {@code queueCapacity} defaults to 0.</b> A {@link ThreadPoolExecutor} only starts threads
 * beyond {@code corePoolSize} once its queue is <i>full</i>. A queue deep enough to hold a whole
 * rubric therefore pins concurrency at {@code corePoolSize} and leaves {@code maxPoolSize}
 * unreachable. With capacity 0 the queue is a {@code SynchronousQueue}: each check is handed straight
 * to a thread, the pool grows to {@code maxPoolSize} under a burst, and anything beyond that runs on
 * the submitting request thread ({@link ThreadPoolExecutor.CallerRunsPolicy}) rather than queueing
 * behind another request's checks.
 */
@Configuration
@ConfigurationProperties("vaas.checks")
@Getter
@Setter
@Slf4j
public class CheckExecutionConfig {

    private static final int MIN_AUTO_CORE_POOL_SIZE = 2;
    private static final int MAX_AUTO_CORE_POOL_SIZE = 8;

    private int corePoolSize = 0;

    private int maxPoolSize = 0;

    private int queueCapacity = 0;

    private int keepAliveSeconds = 60;

    @Bean(name = "checkExecutorPool", destroyMethod = "shutdown")
    public ThreadPoolTaskExecutor checkExecutorPool() {
        validate();

        int core = corePoolSize > 0 ? corePoolSize : autoCorePoolSize();
        int max = maxPoolSize > 0 ? Math.max(maxPoolSize, core) : core * 2;

        if (queueCapacity > 0 && max > core) {
            log.warn("vaas.checks.max-pool-size={} will not be reached: a thread pool only grows past "
                            + "core-pool-size={} once its queue is full, and queue-capacity={} absorbs every check "
                            + "of a typical rubric first. Effective concurrency is {}. Set queue-capacity=0 for "
                            + "direct hand-off, or raise core-pool-size instead.",
                    max, core, queueCapacity, core);
        }

        ThreadPoolTaskExecutor executor = new ThreadPoolTaskExecutor();
        executor.setCorePoolSize(core);
        executor.setMaxPoolSize(max);
        executor.setQueueCapacity(queueCapacity);
        executor.setKeepAliveSeconds(keepAliveSeconds);
        executor.setThreadNamePrefix("check-exec-");

        executor.setRejectedExecutionHandler(new ThreadPoolExecutor.CallerRunsPolicy());

        executor.setWaitForTasksToCompleteOnShutdown(true);
        executor.setAwaitTerminationSeconds(30);
        executor.initialize();
        log.info("Initialized check-execution pool (core={}, max={}, queue={} [{}], keepAlive={}s)",
                core, max, queueCapacity, queueCapacity > 0 ? "bounded" : "direct hand-off", keepAliveSeconds);
        return executor;
    }

    private void validate() {
        if (corePoolSize < 0 || maxPoolSize < 0 || queueCapacity < 0 || keepAliveSeconds < 0) {
            throw new IllegalArgumentException(
                    "vaas.checks pool sizes must not be negative (core-pool-size=" + corePoolSize
                            + ", max-pool-size=" + maxPoolSize + ", queue-capacity=" + queueCapacity
                            + ", keep-alive-seconds=" + keepAliveSeconds + ")");
        }
    }

    private static int autoCorePoolSize() {
        int processors = Runtime.getRuntime().availableProcessors();
        return Math.max(MIN_AUTO_CORE_POOL_SIZE, Math.min(processors, MAX_AUTO_CORE_POOL_SIZE));
    }
}
