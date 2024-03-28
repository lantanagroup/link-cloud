package com.lantanagroup.link.measureeval.consumers;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.annotation.KafkaListener;

public class ResourceAcquiredConsumer {
    private static final Logger log = LoggerFactory.getLogger(ResourceAcquiredConsumer.class);

    @KafkaListener(topics = "ResourceAcquired")
    public void onResourceAcquired(String message) {
        log.info("Received ResourceAcquired: " + message);
    }
}
