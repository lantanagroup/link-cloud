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
                this.process(record);
            } catch (Exception e) {
                if (recoverer != null) {
                    recoverer.accept(record, e);
                } else {
                    logger.error("No recoverer configured; acking and dropping failed record from topic={} partition={} offset={}",
                            record.topic(), record.partition(), record.offset(), e);
                }
            }
            finally {
                ack.acknowledge();
                MDC.remove(MDC_KEY);
            }
        });
    }

    protected abstract void process(ConsumerRecord<K, T> record) throws Exception;

    @PreDestroy
    public void close() {
        // Stop accepting new work, then let in-flight evaluations drain before forcing shutdown.
        executor.shutdown();
        try {
            if (!executor.awaitTermination(Long.MAX_VALUE, TimeUnit.NANOSECONDS)) {
                executor.shutdownNow();
            }
        } catch (InterruptedException e) {
            executor.shutdownNow();
            Thread.currentThread().interrupt();
        }
    }
}
