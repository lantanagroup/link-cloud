package com.lantanagroup.link.measureeval.configs;

import com.azure.storage.common.policy.RequestRetryOptions;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

/**
 * The resource-cache blob client must fail fast: the Kafka retry-topic ladder owns retrying, and
 * the Azure SDK's default policy (4 exponential tries, effectively unbounded per-try timeout) hid
 * ~3 minutes of silent retrying inside every consumer attempt while blocking the single-threaded
 * consumer executor. These tests pin the tuned policy actually handed to the client builder.
 */
class CacheBlobStorageConfigTest {

    @Test
    void retryOptions_failFast_defaults() {
        CacheBlobStorageConfig config = new CacheBlobStorageConfig();

        RequestRetryOptions options = config.buildRetryOptions();

        assertEquals(2, options.getMaxTries(),
                "one quick in-process retry absorbs a dropped connection; anything more belongs to the ladder");
        assertEquals(10, options.getTryTimeoutDuration().toSeconds(),
                "each network attempt must be capped, not left to the SDK's effectively unbounded default");
    }

    @Test
    void retryOptions_areConfigurable() {
        CacheBlobStorageConfig config = new CacheBlobStorageConfig();
        config.setMaxTries(1);
        config.setTryTimeoutSeconds(5);

        RequestRetryOptions options = config.buildRetryOptions();

        assertEquals(1, options.getMaxTries());
        assertEquals(5, options.getTryTimeoutDuration().toSeconds());
    }

    @Test
    void absResourceService_unconfigured_failsAtBoot() {
        // Since LEGLINK-1279 ABS is the durable source for the resource cache: a MeasureEval
        // without blob storage cannot read what eviction removed, so the bean must fail at
        // startup, not degrade to a null service that breaks at the first unlucky message.
        CacheBlobStorageConfig config = new CacheBlobStorageConfig();

        IllegalStateException ex = assertThrows(IllegalStateException.class, config::absResourceService);
        assertTrue(ex.getMessage().contains("resource-cache.blob-storage"),
                "the boot failure must name the missing configuration");
    }
}
