package com.lantanagroup.link.measureeval.configs;

import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
import com.azure.storage.common.policy.RequestRetryOptions;
import com.azure.storage.common.policy.RetryPolicyType;
import com.lantanagroup.link.measureeval.services.AbsResourceService;
import lombok.Getter;
import lombok.Setter;
import org.apache.commons.lang3.StringUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.lang.Nullable;

import java.time.Duration;

@Getter
@Setter
@Configuration
@ConfigurationProperties("resource-cache.blob-storage")
public class CacheBlobStorageConfig {
    private static final Logger logger = LoggerFactory.getLogger(CacheBlobStorageConfig.class);

    private String connectionString;
    private String blobContainerName;
    private String blobRoot;

    /**
     * Azure SDK in-process tries per blob call (initial call + quick retries). The Kafka
     * retry-topic ladder owns real retrying; one extra in-process try only absorbs a dropped
     * connection. The SDK default of 4 exponential tries hid minutes of silent retrying inside
     * every consumer attempt while blocking the single-threaded consumer executor.
     */
    private int maxTries = 2;

    /** Cap on a single network attempt; the SDK default is effectively unbounded. */
    private int tryTimeoutSeconds = 10;

    @Bean
    @Nullable
    public AbsResourceService absResourceService() {
        if (StringUtils.isAnyEmpty(connectionString, blobContainerName)) {
            logger.info("cache-blob-storage not configured, AbsResourceService disabled");
            return null;
        }
        logger.info("Creating AbsResourceService: container={}, blobRoot={}, maxTries={}, tryTimeout={}s",
                blobContainerName, blobRoot, maxTries, tryTimeoutSeconds);
        BlobContainerClient client = new BlobServiceClientBuilder()
                .connectionString(connectionString)
                .retryOptions(buildRetryOptions())
                .buildClient()
                .getBlobContainerClient(blobContainerName);
        return new AbsResourceService(client, blobRoot != null ? blobRoot : "");
    }

    /**
     * Fail-fast policy for the cache blob client: an ABS outage should surface as a quick failure
     * that the retry-topic ladder handles visibly (log lines, attempt headers, dead-lettering),
     * not as silent in-SDK retrying. Package-private so the test pins the exact values.
     */
    RequestRetryOptions buildRetryOptions() {
        return new RequestRetryOptions(
                RetryPolicyType.FIXED,
                maxTries,
                Duration.ofSeconds(tryTimeoutSeconds),
                Duration.ofSeconds(1),
                Duration.ofSeconds(1),
                null);
    }
}
