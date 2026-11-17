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
     * The longest retry delay spring-kafka honors. Its backoff listener (3.3.16; 3.1.4 had no cap) ignores
     * a due-time more than one day ahead and processes the record at once, so a longer delay retries early.
     */
    public static final Duration MAX_RETRY_DELAY = Duration.ofDays(1);

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

    /**
     * Sets the retry schedule, rejecting entries spring-kafka would not wait for, so a bad value fails
     * at startup rather than retrying early.
     *
     * @throws IllegalArgumentException if an entry is null, negative, or longer than {@link #MAX_RETRY_DELAY}
     */
    public void setConsumerRetryDuration(List<Duration> consumerRetryDuration) {
        if (consumerRetryDuration != null) {
            for (Duration delay : consumerRetryDuration) {
                validateDelay(delay);
            }
        }
        this.consumerRetryDuration = consumerRetryDuration;
    }

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

    private static void validateDelay(Duration delay) {
        if (delay == null || delay.isNegative()) {
            throw new IllegalArgumentException(
                    "spring.kafka.retry.consumer-retry-duration entries must be zero or positive, but got " + delay);
        }
        if (delay.compareTo(MAX_RETRY_DELAY) > 0) {
            throw new IllegalArgumentException(
                    "spring.kafka.retry.consumer-retry-duration entries must be at most " + MAX_RETRY_DELAY +
                    ", because spring-kafka retries a longer delay immediately, but got " + delay);
        }
    }

}
