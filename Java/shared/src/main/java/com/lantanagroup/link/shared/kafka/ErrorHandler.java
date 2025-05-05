package com.lantanagroup.link.shared.kafka;

import jakarta.annotation.Nonnull;
import org.apache.kafka.clients.consumer.Consumer;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.listener.CommonErrorHandler;
import org.springframework.kafka.listener.MessageListenerContainer;
import org.springframework.kafka.support.KafkaUtils;

public class ErrorHandler implements CommonErrorHandler {
    private static final Logger logger = LoggerFactory.getLogger(ErrorHandler.class);

    @Override
    public boolean handleOne(
            @Nonnull Exception exception,
            @Nonnull ConsumerRecord<?, ?> record,
            @Nonnull Consumer<?, ?> consumer,
            @Nonnull MessageListenerContainer container) {
        logger.error("Failed to consume record: {}", KafkaUtils.format(record), exception);
        return true;
    }
}
