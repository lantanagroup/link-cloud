package com.lantanagroup.link.shared.config;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;

import java.time.Duration;
import java.util.List;

@ConfigurationProperties(prefix = "spring.kafka.retry")
@Getter
@Setter
public class KafkaRetryConfig {

    private int maxAttempts = 3;

    private long retryBackoffMs = 3000;


    private boolean disableRetryConsumer = false;


    private List<Duration> consumerRetryDuration = List.of();


    public boolean isArrayDrivenBackoff() {
        return consumerRetryDuration != null && !consumerRetryDuration.isEmpty();
    }

    public int effectiveMaxAttempts() {
        return isArrayDrivenBackoff() ? consumerRetryDuration.size() + 1 : maxAttempts;
    }

    public long backoffMsForAttempt(int attempt) {
        if (!isArrayDrivenBackoff()) {
            return retryBackoffMs;
        }
        int idx = Math.min(Math.max(attempt - 1, 0), consumerRetryDuration.size() - 1);
        return consumerRetryDuration.get(idx).toMillis();
    }

}
