package com.lantanagroup.link.shared.kafka;

import jakarta.annotation.PreDestroy;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.slf4j.MDC;
import org.springframework.kafka.listener.AcknowledgingMessageListener;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.support.Acknowledgment;
import org.springframework.kafka.support.KafkaUtils;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

public abstract class AsyncListener<K, V> implements AcknowledgingMessageListener<K, V> {
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final ConsumerRecordRecoverer recoverer;

    protected AsyncListener(ConsumerRecordRecoverer recoverer) {
        this.recoverer = recoverer;
    }

    protected AsyncListener() {
        this(null);
    }

    @Override
    public void onMessage(ConsumerRecord<K, V> record, Acknowledgment ack) {
        executor.submit(() -> {
            final String MDC_KEY = "record";
            try {
                MDC.put(MDC_KEY, KafkaUtils.format(record));
                process(record);
            } catch (Exception e) {
                if (recoverer != null) {
                    recoverer.accept(record, e);
                }
            } finally {
                ack.acknowledge();
                MDC.remove(MDC_KEY);
            }
        });
    }

    protected abstract void process(ConsumerRecord<K, V> record) throws Exception;

    @PreDestroy
    public void close() {
        executor.shutdownNow();
        try {
            executor.awaitTermination(Long.MAX_VALUE, TimeUnit.NANOSECONDS);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }
}
