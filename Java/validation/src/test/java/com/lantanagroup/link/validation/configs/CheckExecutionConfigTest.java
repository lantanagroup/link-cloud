package com.lantanagroup.link.validation.configs;

import org.junit.jupiter.api.Test;
import org.springframework.scheduling.concurrent.ThreadPoolTaskExecutor;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.SynchronousQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Guards the pool wiring, in particular that {@code max-pool-size} is actually reachable. A thread pool
 * only grows past its core size once the queue is full, so a deep queue silently caps concurrency at
 * {@code core-pool-size} and turns {@code max-pool-size} into dead configuration.
 */
class CheckExecutionConfigTest {

    @Test
    void defaultsAutoSizeFromProcessorsAndHandOffDirectly() {
        ThreadPoolTaskExecutor executor = null;
        try {
            executor = new CheckExecutionConfig().checkExecutorPool();
            ThreadPoolExecutor pool = executor.getThreadPoolExecutor();

            int core = pool.getCorePoolSize();
            assertTrue(core >= 2 && core <= 8, "core pool size should be clamped to [2, 8] but was " + core);
            assertEquals(core * 2, pool.getMaximumPoolSize());
            assertInstanceOf(SynchronousQueue.class, pool.getQueue(),
                    "queue-capacity 0 must hand off directly so the pool can grow to max-pool-size");
            assertInstanceOf(ThreadPoolExecutor.CallerRunsPolicy.class, pool.getRejectedExecutionHandler());
        } finally {
            if (executor != null) {
                executor.shutdown();
            }
        }
    }

    @Test
    void explicitSizesAreHonoured() {
        CheckExecutionConfig config = new CheckExecutionConfig();
        config.setCorePoolSize(5);
        config.setMaxPoolSize(10);

        ThreadPoolTaskExecutor executor = config.checkExecutorPool();
        try {
            assertEquals(5, executor.getThreadPoolExecutor().getCorePoolSize());
            assertEquals(10, executor.getThreadPoolExecutor().getMaximumPoolSize());
        } finally {
            executor.shutdown();
        }
    }

    @Test
    void positiveQueueCapacityStillBuildsABoundedQueue() {
        CheckExecutionConfig config = new CheckExecutionConfig();
        config.setCorePoolSize(2);
        config.setMaxPoolSize(4);
        config.setQueueCapacity(50);

        ThreadPoolTaskExecutor executor = config.checkExecutorPool();
        try {
            LinkedBlockingQueue<?> queue =
                    assertInstanceOf(LinkedBlockingQueue.class, executor.getThreadPoolExecutor().getQueue());
            assertEquals(50, queue.remainingCapacity());
        } finally {
            executor.shutdown();
        }
    }

    @Test
    void maxPoolSizeIsNeverBelowCorePoolSize() {
        CheckExecutionConfig config = new CheckExecutionConfig();
        config.setCorePoolSize(6);
        config.setMaxPoolSize(2);

        ThreadPoolTaskExecutor executor = config.checkExecutorPool();
        try {
            assertEquals(6, executor.getThreadPoolExecutor().getCorePoolSize());
            assertEquals(6, executor.getThreadPoolExecutor().getMaximumPoolSize());
        } finally {
            executor.shutdown();
        }
    }

    @Test
    void negativeSizesAreRejected() {
        CheckExecutionConfig config = new CheckExecutionConfig();
        config.setCorePoolSize(-1);

        IllegalArgumentException thrown = assertThrows(IllegalArgumentException.class, config::checkExecutorPool);
        assertTrue(thrown.getMessage().contains("core-pool-size=-1"), thrown.getMessage());
    }

    @Test
    void poolGrowsToMaxPoolSizeUnderABurst() throws Exception {
        CheckExecutionConfig config = new CheckExecutionConfig();
        config.setCorePoolSize(1);
        config.setMaxPoolSize(4);

        ThreadPoolTaskExecutor executor = config.checkExecutorPool();
        try {
            CountDownLatch release = new CountDownLatch(1);
            CountDownLatch started = new CountDownLatch(4);
            for (int i = 0; i < 4; i++) {
                executor.execute(() -> {
                    started.countDown();
                    try {
                        release.await();
                    } catch (InterruptedException e) {
                        Thread.currentThread().interrupt();
                    }
                });
            }
            assertTrue(started.await(10, TimeUnit.SECONDS),
                    "all four tasks should be running at once, so the pool must have grown past its core size");
            assertEquals(4, executor.getThreadPoolExecutor().getPoolSize());
            release.countDown();
        } finally {
            executor.shutdown();
        }
    }
}
