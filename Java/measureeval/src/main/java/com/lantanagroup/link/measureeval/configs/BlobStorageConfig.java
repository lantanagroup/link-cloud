package com.lantanagroup.link.measureeval.configs;

import ca.uhn.fhir.context.FhirContext;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
import com.lantanagroup.link.measureeval.services.BlobStorageService;
import com.lantanagroup.link.measureeval.services.MeasureReportGeneratedProducer;
import com.lantanagroup.link.shared.services.ReportClient;
import lombok.Getter;
import lombok.Setter;
import org.apache.commons.lang3.StringUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Getter
@Setter
@Configuration
@ConfigurationProperties("internal-blob-storage")
public class BlobStorageConfig {
    private static final Logger logger = LoggerFactory.getLogger(BlobStorageConfig.class);

    private String connectionString;
    private String blobContainerName;

    @Bean
    public BlobContainerClient blobContainerClient() {
        if (StringUtils.isAnyEmpty(connectionString, blobContainerName)) {
            throw new IllegalStateException("Missing required internal-blob-storage configuration: connectionString and blobContainerName must be set.");
        }
        logger.info("Creating BlobContainerClient with containerName={}", blobContainerName);
        BlobContainerClient client = new BlobServiceClientBuilder()
                .connectionString(connectionString)
                .buildClient()
                .getBlobContainerClient(blobContainerName);
        logger.info("BlobContainerClient account URL: {}", client.getAccountUrl());
        return client;
    }

    @Bean
    public BlobStorageService blobStorageService(BlobContainerClient blobContainerClient, FhirContext fhirContext, ReportClient reportClient, MeasureReportGeneratedProducer measureReportGeneratedProducer) {
        return new BlobStorageService(blobContainerClient, blobContainerName, fhirContext, reportClient, measureReportGeneratedProducer);
    }

}
