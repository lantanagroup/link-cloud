package com.lantanagroup.link.shared.kafka;

import org.apache.kafka.clients.consumer.Consumer;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.listener.CommonErrorHandler;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.listener.MessageListenerContainer;

public class ErrorHandler implements CommonErrorHandler {
    private static final Logger logger = LoggerFactory.getLogger(ErrorHandler.class);

    private final ConsumerRecordRecoverer recoverer;

    public ErrorHandler(ConsumerRecordRecoverer recoverer) {
        this.recoverer = recoverer;
    }

    @Override
    public boolean handleOne(Exception exception, ConsumerRecord<?, ?> record,
                             Consumer<?, ?> consumer, MessageListenerContainer container) {
        logger.error("Container-thread error for record from topic={} partition={} offset={}; routing to error topic",
                record.topic(), record.partition(), record.offset(), exception);
        try {
            recoverer.accept(record, exception);
        } catch (Exception ex) {
            // Publishing to the error topic failed; do NOT mark as handled, so the record is
            // redelivered rather than silently lost.
            logger.error("Failed to route record to error topic from topic={} partition={} offset={}",
                    record.topic(), record.partition(), record.offset(), ex);
            return false;
        }
        return true;
    }

    @Override
    public void handleOtherException(Exception exception, Consumer<?, ?> consumer,
                                     MessageListenerContainer container, boolean batchListener) {
        logger.error("Non-record error in listener container", exception);
    }
}
