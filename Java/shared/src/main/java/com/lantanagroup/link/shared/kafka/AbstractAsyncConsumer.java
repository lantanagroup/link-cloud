package com.lantanagroup.link.shared.kafka;

import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.support.Acknowledgment;
import org.springframework.kafka.support.KafkaUtils;

/**
 * Runs {@link #process} and the acknowledgment on the caller thread. The listener returns only after
 * the record is acked, so a rebalance cannot acknowledge a record this thread has not finished.
 */
public abstract class AbstractAsyncConsumer<K, T> {

    private static final Logger logger = LoggerFactory.getLogger(AbstractAsyncConsumer.class);

    private final ConsumerRecordRecoverer recoverer;

    protected AbstractAsyncConsumer(ConsumerRecordRecoverer recoverer) {
        this.recoverer = recoverer;
    }

    protected AbstractAsyncConsumer() {
        this.recoverer = null;
    }

    protected void doConsume(ConsumerRecord<K, T> record, Acknowledgment ack) {
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
                    // Route to -Retry/-Error, never back onto the source topic. If this throws (e.g. the
                    // broker is unavailable), skip the ack below. The missing acknowledgment leaves the
                    // offset uncommitted so the record is redelivered after a restart or rebalance.
                    recoverer.accept(record, processError);
                }
            }
            // Reached only when process() succeeded, recovery published successfully, or we
            // intentionally dropped (no recoverer): the record is accounted for. (Do not use nack()
            // here — unsupported with asyncAcks.)
            ack.acknowledge();
        } catch (Exception unrecovered) {
            // process() failed AND recovery (or the ack itself) failed. Do NOT acknowledge: the
            // missing ack leaves the offset uncommitted, preserving the record (at-least-once, no loss)
            // until a restart or rebalance redelivers it — this log line is the operational signal.
            logger.error("Failed to recover or acknowledge record from topic={} partition={} offset={}; leaving offset uncommitted for redelivery",
                    record.topic(), record.partition(), record.offset(), unrecovered);
        } finally {
            MDC.remove(MDC_KEY);
        }
    }

    protected abstract void process(ConsumerRecord<K, T> record) throws Exception;
}
