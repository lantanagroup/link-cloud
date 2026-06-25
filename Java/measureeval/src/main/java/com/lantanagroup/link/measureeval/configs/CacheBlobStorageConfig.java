package com.lantanagroup.link.measureeval.configs;

import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
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

@Getter
@Setter
@Configuration
@ConfigurationProperties("resource-cache.blob-storage")
public class CacheBlobStorageConfig {
    private static final Logger logger = LoggerFactory.getLogger(CacheBlobStorageConfig.class);

    private String connectionString;
    private String blobContainerName;
    private String blobRoot;

    @Bean
    @Nullable
    public AbsResourceService absResourceService() {
        if (StringUtils.isAnyEmpty(connectionString, blobContainerName)) {
            logger.info("cache-blob-storage not configured, AbsResourceService disabled");
            return null;
        }
        logger.info("Creating AbsResourceService: container={}, blobRoot={}", blobContainerName, blobRoot);
        BlobContainerClient client = new BlobServiceClientBuilder()
                .connectionString(connectionString)
                .buildClient()
                .getBlobContainerClient(blobContainerName);
        return new AbsResourceService(client, blobRoot != null ? blobRoot : "");
    }
}
