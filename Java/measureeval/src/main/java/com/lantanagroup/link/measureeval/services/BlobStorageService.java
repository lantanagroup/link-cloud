package com.lantanagroup.link.measureeval.services;

import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
import com.lantanagroup.link.measureeval.configs.BlobStorageConfig;
import org.springframework.stereotype.Service;

import java.io.ByteArrayInputStream;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;

@Service
public class BlobStorageService {

    private final BlobContainerClient containerClient;

    public BlobStorageService(BlobStorageConfig config) {
        BlobServiceClient blobServiceClient = new BlobServiceClientBuilder()
                .connectionString(config.getConnectionString())
                .buildClient();
        this.containerClient = blobServiceClient.getBlobContainerClient(config.getBlobContainerName());
        if (!this.containerClient.exists()) {
            this.containerClient.create();
        }
    }

    public String uploadPayload(String fileName, String content) {
        BlobClient blobClient = containerClient.getBlobClient(fileName);
        try (InputStream dataStream = new ByteArrayInputStream(content.getBytes(StandardCharsets.UTF_8))) {
            blobClient.upload(dataStream, content.length(), true);
        } catch (Exception e) {
            throw new RuntimeException("Failed to upload payload to blob storage", e);
        }
        return blobClient.getBlobUrl();
    }
}
