package com.lantanagroup.link.validation.kafka;

import org.apache.kafka.clients.consumer.Consumer;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.common.errors.RecordDeserializationException;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.listener.DefaultErrorHandler;
import org.springframework.kafka.listener.MessageListenerContainer;
import org.springframework.util.backoff.FixedBackOff;

public class KafkaErrorHandler extends DefaultErrorHandler {
    private static final Logger log = LoggerFactory.getLogger(KafkaErrorHandler.class);

    public KafkaErrorHandler() {
        super(new FixedBackOff(10000, 2));
    }

    @Override
    public boolean seeksAfterHandling() {
        return true;
    }

    @Override
    public boolean handleOne(Exception exception, ConsumerRecord<?, ?> record, Consumer<?, ?> consumer, MessageListenerContainer container) {
        handle(exception, consumer);
        return true;
    }

    @Override
    public void handleOtherException(Exception exception, Consumer<?, ?> consumer, MessageListenerContainer container, boolean batchListener) {
        handle(exception, consumer);
    }

    private void handle(Exception exception, Consumer<?, ?> consumer) {
        if (exception instanceof RecordDeserializationException ex) {
            log.error("Kafka deserialization exception not handled: {}", exception.getMessage());
            consumer.seek(ex.topicPartition(), ex.offset() + 1L);
            consumer.commitSync();
        } else {
            log.error("Exception not handled", exception);
        }
    }
}
