package com.lantanagroup.link.measureeval.configs;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.services.BlobStorageService;
import com.lantanagroup.link.measureeval.services.MeasureReportGeneratedProducer;
import com.lantanagroup.link.shared.services.ReportClient;
import lombok.Getter;
import lombok.Setter;
import org.apache.commons.lang3.StringUtils;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Getter
@Setter
@Configuration
@ConfigurationProperties("internal-blob-storage")
public class BlobStorageConfig {
    private String connectionString;
    private String blobContainerName;

    @Bean
    public BlobStorageService blobStorageService(FhirContext fhirContext, ReportClient reportClient, MeasureReportGeneratedProducer measureReportGeneratedProducer) {
        if (StringUtils.isAnyEmpty(connectionString, blobContainerName)) {
            throw new IllegalStateException("Missing required internal-blob-storage configuration: connectionString and blobContainerName must be set.");
        }
        return new BlobStorageService(connectionString, blobContainerName, fhirContext, reportClient, measureReportGeneratedProducer);
    }
}
