package com.lantanagroup.link.measureeval.consumers;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.annotation.KafkaListener;

public class ResourceNormalizedConsumer {
    private static final Logger log = LoggerFactory.getLogger(ResourceNormalizedConsumer.class);

    @KafkaListener(topics = "ResourceNormalized")
    public void onResourceNormalized(String message) {
        log.info("Received ResourceNormalized: " + message);
    }

    @KafkaListener(topics = "ResourceNormalized-Error")
    public void onResourceNormalizedError(String message) {
        log.info("Received ResourceNormalized-Error: " + message);
    }
}
