package com.lantanagroup.link.shared.kafka;

import jakarta.annotation.PreDestroy;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.support.Acknowledgment;
import org.springframework.kafka.support.KafkaUtils;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;


public abstract class AbstractAsyncConsumer<K, T> {

    private static final Logger logger = LoggerFactory.getLogger(AbstractAsyncConsumer.class);

    // Bounded drain window on shutdown: long enough for an in-flight evaluation to finish, but never
    // unbounded — a single hung task must not block service shutdown.
    private static final long SHUTDOWN_GRACE_SECONDS = 30;

    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final ConsumerRecordRecoverer recoverer;

    protected AbstractAsyncConsumer(ConsumerRecordRecoverer recoverer) {
        this.recoverer = recoverer;
    }

    protected AbstractAsyncConsumer() {
        this.recoverer = null;
    }

    protected void doConsume(ConsumerRecord<K, T> record, Acknowledgment ack) {
        executor.submit(() -> {
            final String MDC_KEY = "record";
            try {
                MDC.put(MDC_KEY, KafkaUtils.format(record));
                try {
                    this.process(record);
                } catch (Exception processError) {
                    if (recoverer == null) {
                        // No recovery path configured: intentionally ack-and-drop the failed record.
                        logger.error("No recoverer configured; acking and dropping failed record from topic={} partition={} offset={}",
                                record.topic(), record.partition(), record.offset(), processError);
                    } else {
                        // Route to -Retry/-Error. If this throws (e.g. the broker is unavailable), let it
                        // propagate so the ack below is skipped. Under MANUAL_IMMEDIATE + asyncAcks the
                        // missing acknowledgment stalls this partition (no further records are committed
                        // past the gap) rather than silently losing a failure that was neither retried nor
                        // dead-lettered; the record is redelivered after a restart or rebalance.
                        recoverer.accept(record, processError);
                    }
                }
                // Reached only when process() succeeded, recovery published successfully, or we
                // intentionally dropped (no recoverer): the record is accounted for. With asyncAcks the
                // acknowledgment is queued from this executor thread and the container commits it in
                // offset order, not immediately. (Do not use nack() here — unsupported with asyncAcks.)
                ack.acknowledge();
            } catch (Exception unrecovered) {
                // process() failed AND recovery (or the ack itself) failed. Do NOT acknowledge: the
                // missing ack pauses this partition, preserving the record (at-least-once, no loss) at
                // the cost of a stalled partition until a restart or rebalance redelivers it — this log
                // line is the operational signal for that condition.
                logger.error("Failed to recover or acknowledge record from topic={} partition={} offset={}; leaving offset uncommitted for redelivery",
                        record.topic(), record.partition(), record.offset(), unrecovered);
            } finally {
                MDC.remove(MDC_KEY);
            }
        });
    }

    protected abstract void process(ConsumerRecord<K, T> record) throws Exception;

    @PreDestroy
    public void close() {
        // Stop accepting new work, then let in-flight evaluations drain within a bounded window before
        // forcing shutdown — a single hung task must not block service shutdown indefinitely.
        executor.shutdown();
        try {
            if (!executor.awaitTermination(SHUTDOWN_GRACE_SECONDS, TimeUnit.SECONDS)) {
                logger.warn("Timed out waiting for async consumer executor to drain; forcing shutdown.");
                executor.shutdownNow();
            }
        } catch (InterruptedException e) {
            executor.shutdownNow();
            Thread.currentThread().interrupt();
        }
    }
}
