package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.validation.services.BlobStorageService;
import lombok.Getter;
import lombok.Setter;
import org.apache.commons.lang3.StringUtils;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Getter
@Setter
@Configuration
@ConfigurationProperties("blob-storage")
public class BlobStorageConfig {
    private String connectionString;
    private String blobContainerName;

    @Bean
    public BlobStorageService blobStorageService() {
        if (StringUtils.isAnyEmpty(connectionString, blobContainerName)) {
            return null;
        }
        return new BlobStorageService(connectionString, blobContainerName);
    }
}
