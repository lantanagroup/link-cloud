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

    /**
     * Per-attempt retry delays — the single source of truth for retry depth and timing. Entry K-1 is the
     * delay before retry K, and the total attempt count is derived from the list size
     * ({@link #effectiveMaxAttempts()} = size + 1); after the last entry the record is dead-lettered to
     * {@code -Error}. An empty list disables retries (the first failure is dead-lettered immediately).
     *
     * <p>The default — two 3-second delays, i.e. 3 attempts — preserves the prior behavior for services
     * that do not configure a schedule.</p>
     */
    private List<Duration> consumerRetryDuration = List.of(Duration.ofSeconds(3), Duration.ofSeconds(3));

    private boolean disableRetryConsumer = false;

    public int effectiveMaxAttempts() {
        return (consumerRetryDuration == null ? 0 : consumerRetryDuration.size()) + 1;
    }

    public long backoffMsForAttempt(int attempt) {
        if (consumerRetryDuration == null || consumerRetryDuration.isEmpty()) {
            return 0L;
        }
        int idx = Math.min(Math.max(attempt - 1, 0), consumerRetryDuration.size() - 1);
        return consumerRetryDuration.get(idx).toMillis();
    }

}
