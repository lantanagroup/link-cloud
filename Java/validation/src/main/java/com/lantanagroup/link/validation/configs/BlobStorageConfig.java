package com.lantanagroup.link.validation.configs;

import com.azure.storage.common.policy.RequestRetryOptions;
import com.azure.storage.common.policy.RetryPolicyType;
import com.lantanagroup.link.validation.services.BlobStorageService;
import lombok.Getter;
import lombok.Setter;
import org.apache.commons.lang3.StringUtils;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.time.Duration;

@Getter
@Setter
@Configuration
@ConfigurationProperties("internal-blob-storage")
public class BlobStorageConfig {
    private String connectionString;
    private String blobContainerName;

    /**
     * Azure SDK in-process tries per blob call (initial call + quick retries). This client is used
     * entirely on the consumer path — the bundle download and the pre-qual append both run inside
     * process() — where the Kafka retry-topic ladder owns real retrying. The SDK default of 4
     * exponential tries hid minutes of silent retrying inside every consumer attempt while blocking
     * the single-threaded consumer executor.
     */
    private int maxTries = 2;

    /** Cap on a single network attempt; the SDK default is effectively unbounded. */
    private int tryTimeoutSeconds = 10;

    @Bean
    public BlobStorageService blobStorageService() {
        if (StringUtils.isAnyEmpty(connectionString, blobContainerName)) {
            return null;
        }
        return new BlobStorageService(connectionString, blobContainerName, buildRetryOptions());
    }

    /**
     * Fail-fast policy: an ABS outage should surface as a quick failure the retry-topic ladder
     * handles visibly (log lines, attempt headers, dead-lettering), not as silent in-SDK retrying.
     * Package-private so the test pins the exact values.
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
