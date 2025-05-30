package com.lantanagroup.link.shared.health;

import com.lantanagroup.link.shared.kafka.Properties;
import com.lantanagroup.link.shared.kafka.Topics;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Component;

import java.time.ZoneId;
import java.time.ZonedDateTime;
import java.time.format.DateTimeFormatter;
import java.util.concurrent.TimeUnit;

@Component("kafka")
public class KafkaHealthCheckIndicator implements HealthIndicator {

    private final KafkaTemplate<String, String> kafkaTemplate;

    private final String serviceName;

    public KafkaHealthCheckIndicator(KafkaTemplate<String, String> healthKafkaTemplate, @Value(" ${spring.application.name}") String serviceName) {
        this.kafkaTemplate = healthKafkaTemplate;
        this.serviceName = serviceName;
    }

    @Override
    public Health health() {
        // Perform a health check on Kafka
        boolean isKafkaHealthy = checkKafkaConnection();
        if (isKafkaHealthy) {
            return Health.up().withDetail("Kafka", "Available").build();
        } else {
            return Health.down().withDetail("Kafka", "Unavailable").build();
        }
    }

    private boolean checkKafkaConnection() {
        try {
            // format the message to send to Health Kafka topic
            ZonedDateTime utcDate = ZonedDateTime.now(ZoneId.of("UTC"));
            String formattedDate = utcDate.format(DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss"));
            String message = String.format("Service health check on %s (UTC)", formattedDate);

            // the send() call itself is non-blocking so we need a get to force the program to wait till the broker acknowledges the message or throws an exception if Kafka is down or the timeout expires
            // the timeout on get should be >= MAX_BLOCK_MS_CONFIG configured on the ProducerConfig properties of KafkaTemplate; add another 1000 millis to the configured MAX_BLOCK_MS_CONFIG property
            long getTimeOut  = Properties.MAX_BLOCK_MS_CONFIG + 1000; // in millis
            kafkaTemplate.send(Topics.SERVICE_HEALTH_CHECK, serviceName, message).get(getTimeOut, TimeUnit.MILLISECONDS);
            return true;
        } catch (Exception e) {
            return false;
        }
    }
}
