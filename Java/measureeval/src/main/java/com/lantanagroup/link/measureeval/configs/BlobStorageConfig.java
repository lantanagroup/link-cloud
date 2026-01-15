package com.lantanagroup.link.measureeval.configs;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConfigurationProperties(prefix = "link.measure-eval.blob-storage")
@Getter
@Setter
public class BlobStorageConfig {
    private String connectionString;
    private String containerName;
}
