package com.lantanagroup.link.shared.kafka;

import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.junit.jupiter.api.Test;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.support.Acknowledgment;

import java.util.concurrent.atomic.AtomicReference;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;

class AbstractAsyncConsumerTest {

    @Test
    void processAndAckRunOnTheCallerThreadBeforeReturn() {
        AtomicReference<Thread> processThread = new AtomicReference<>();
        AbstractAsyncConsumer<String, String> consumer = new AbstractAsyncConsumer<>() {
            @Override
            protected void process(ConsumerRecord<String, String> record) {
                processThread.set(Thread.currentThread());
            }
        };
        Acknowledgment ack = mock(Acknowledgment.class);

        consumer.doConsume(record(), ack);

        assertSame(Thread.currentThread(), processThread.get());
        verify(ack).acknowledge();
    }

    @Test
    void recoveryFailureDoesNotAckAndDoesNotThrow() {
        ConsumerRecordRecoverer recoverer = mock(ConsumerRecordRecoverer.class);
        doThrow(new RuntimeException("publish failed")).when(recoverer).accept(any(), any());
        AbstractAsyncConsumer<String, String> consumer = new AbstractAsyncConsumer<>(recoverer) {
            @Override
            protected void process(ConsumerRecord<String, String> record) {
                throw new IllegalStateException("boom");
            }
        };
        Acknowledgment ack = mock(Acknowledgment.class);

        assertDoesNotThrow(() -> consumer.doConsume(record(), ack));

        verify(ack, never()).acknowledge();
    }

    @Test
    void successfulRecoveryAcks() {
        ConsumerRecordRecoverer recoverer = mock(ConsumerRecordRecoverer.class);
        AbstractAsyncConsumer<String, String> consumer = new AbstractAsyncConsumer<>(recoverer) {
            @Override
            protected void process(ConsumerRecord<String, String> record) {
                throw new IllegalStateException("boom");
            }
        };
        Acknowledgment ack = mock(Acknowledgment.class);
        ConsumerRecord<String, String> record = record();

        consumer.doConsume(record, ack);

        verify(recoverer).accept(any(), any(IllegalStateException.class));
        verify(ack).acknowledge();
    }

    @Test
    void noRecovererAcksAndDrops() {
        AbstractAsyncConsumer<String, String> consumer = new AbstractAsyncConsumer<>() {
            @Override
            protected void process(ConsumerRecord<String, String> record) {
                throw new IllegalStateException("boom");
            }
        };
        Acknowledgment ack = mock(Acknowledgment.class);

        assertDoesNotThrow(() -> consumer.doConsume(record(), ack));

        verify(ack).acknowledge();
    }

    private static ConsumerRecord<String, String> record() {
        return new ConsumerRecord<>("topic", 0, 1L, "fac:pat", "value");
    }
}
