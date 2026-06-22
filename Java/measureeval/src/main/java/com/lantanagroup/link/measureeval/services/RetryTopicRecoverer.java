package com.lantanagroup.link.measureeval.services;

import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.common.header.Headers;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.kafka.listener.DeadLetterPublishingRecoverer;
import org.springframework.kafka.retrytopic.RetryTopicHeaders;

import java.math.BigInteger;
import java.nio.ByteBuffer;

public class RetryTopicRecoverer implements ConsumerRecordRecoverer {

    private static final Logger logger = LoggerFactory.getLogger(RetryTopicRecoverer.class);

    private final int maxAttempts;
    private final long backoffMs;
    private final DeadLetterPublishingRecoverer delegate;

    public RetryTopicRecoverer(int maxAttempts, long backoffMs, DeadLetterPublishingRecoverer delegate) {
        this.maxAttempts = maxAttempts;
        this.backoffMs = backoffMs;
        this.delegate = delegate;
    }

    @Override
    public void accept(ConsumerRecord<?, ?> record, Exception exception) {
        Headers headers = record.headers();

        int attempt = currentAttempts(headers) + 1;
        headers.remove(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS);
        headers.add(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS, ByteBuffer.allocate(4).putInt(attempt).array());

        long newBackoff = System.currentTimeMillis() + backoffMs;
        headers.remove(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP);
        headers.add(RetryTopicHeaders.DEFAULT_HEADER_BACKOFF_TIMESTAMP, BigInteger.valueOf(newBackoff).toByteArray());
        if (headers.lastHeader(RetryTopicHeaders.DEFAULT_HEADER_ORIGINAL_TIMESTAMP) == null) {
            headers.add(RetryTopicHeaders.DEFAULT_HEADER_ORIGINAL_TIMESTAMP, BigInteger.valueOf(record.timestamp()).toByteArray());
        }

        // Exception headers are stamped by the DeadLetterPublishingRecoverer delegate on publish.
        if (attempt >= maxAttempts) {
            logger.warn("Max retry attempts ({}) reached for topic [{}]. Routing to DLT.",
                    maxAttempts, record.topic());
        } else {
            logger.info("Retry attempt {}/{} for topic [{}].",
                    attempt, maxAttempts, record.topic());
        }

        delegate.accept(record, exception);
    }

    /**
     * Retry attempts recorded in the attempts header, or 0 if absent. Shared with KafkaConfig's
     * destination resolver so routing and stamping use the same count.
     */
    public static int currentAttempts(Headers headers) {
        var header = headers.lastHeader(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS);
        if (header == null) return 0;
        return ByteBuffer.wrap(header.value()).getInt();
    }
}
