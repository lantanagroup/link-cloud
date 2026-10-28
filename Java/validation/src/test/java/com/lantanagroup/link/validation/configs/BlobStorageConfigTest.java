package com.lantanagroup.link.validation.configs;

import com.azure.storage.common.policy.RequestRetryOptions;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

/**
 * The validation blob client is used entirely on the consumer path (the bundle download and the
 * pre-qual append both run inside process()), where the Kafka retry-topic ladder owns retrying.
 * The Azure SDK's default policy (4 exponential tries, effectively unbounded per-try timeout) hides
 * minutes of silent retrying inside every consumer attempt during an ABS outage while blocking the
 * single-threaded consumer executor. These tests pin the fail-fast policy handed to the builder.
 */
class BlobStorageConfigTest {

    @Test
    void retryOptions_failFast_defaults() {
        BlobStorageConfig config = new BlobStorageConfig();

        RequestRetryOptions options = config.buildRetryOptions();

        assertEquals(2, options.getMaxTries(),
                "one quick in-process retry absorbs a dropped connection; anything more belongs to the ladder");
        assertEquals(10, options.getTryTimeoutDuration().toSeconds(),
                "each network attempt must be capped, not left to the SDK's effectively unbounded default");
    }

    @Test
    void retryOptions_areConfigurable() {
        BlobStorageConfig config = new BlobStorageConfig();
        config.setMaxTries(1);
        config.setTryTimeoutSeconds(5);

        RequestRetryOptions options = config.buildRetryOptions();

        assertEquals(1, options.getMaxTries());
        assertEquals(5, options.getTryTimeoutDuration().toSeconds());
    }
}
