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
 * Dedicated thread pool for parallel rubric check execution.
 * Used only when {@code vaas.checks.parallel} is enabled; threads are created
 * lazily, so no {@code check-exec-} threads exist when the feature is disabled.
 *
 * Kept separate from HAPI's {@link java.util.concurrent.ForkJoinPool#commonPool()}
 * to avoid competing for the same thread pool, although both ultimately share
 * the same CPU cores.
 *
 * {@code queueCapacity} defaults to 0 so checks are handed directly to threads,
 * allowing the pool to grow to {@code maxPoolSize}. When the pool is saturated,
 * {@link ThreadPoolExecutor.CallerRunsPolicy} runs additional checks on the
 * submitting request thread instead of queueing them.
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
