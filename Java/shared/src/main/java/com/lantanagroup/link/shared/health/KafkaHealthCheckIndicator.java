package com.lantanagroup.link.shared.health;

import com.lantanagroup.link.shared.kafka.Properties;
import com.lantanagroup.link.shared.kafka.Topics;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.actuate.health.Health;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Component;

import java.time.ZoneId;
import java.time.ZonedDateTime;
import java.time.format.DateTimeFormatter;
import java.util.concurrent.TimeUnit;

@Component("Kafka")
public class KafkaHealthCheckIndicator extends TimedHealthIndicator {

    private final KafkaTemplate<String, String> kafkaTemplate;

    private final String serviceName;

    /**
     * Backstop deadline for the acknowledgement, configurable via
     * {@code management.health.kafka.timeout-ms}. Keep it above the health producer's
     * {@link Properties#MAX_BLOCK_MS_CONFIG} so the producer gives up first and reports why,
     * and keep the two together well inside the Admin BFF's health-check window -- it is shared with the
     * other indicators, which Spring runs one after another on this same request thread.
     */
    private final long checkTimeoutMs;

    /** Default for {@code management.health.kafka.timeout-ms}; a String so it can sit in the annotation below. */
    static final String DEFAULT_TIMEOUT_MS = "1500";

    public KafkaHealthCheckIndicator(KafkaTemplate<String, String> healthKafkaTemplate,
                                     @Value(" ${spring.application.name}") String serviceName,
                                     @Value("${management.health.kafka.timeout-ms:" + DEFAULT_TIMEOUT_MS + "}") long checkTimeoutMs) {
        this.kafkaTemplate = healthKafkaTemplate;
        this.serviceName = serviceName;
        this.checkTimeoutMs = checkTimeoutMs;
    }

    @Override
    protected Health check() {
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
            // the timeout on get stays above MAX_BLOCK_MS_CONFIG configured on the ProducerConfig properties of the health KafkaTemplate, so the producer is the one that gives up first
            kafkaTemplate.send(Topics.SERVICE_HEALTH_CHECK, serviceName, message).get(checkTimeoutMs, TimeUnit.MILLISECONDS);
            return true;
        } catch (Exception e) {
            return false;
        }
    }
}
