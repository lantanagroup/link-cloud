package com.lantanagroup.link.validation.services;

import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClient;
import com.azure.storage.blob.BlobServiceClientBuilder;

public class BlobStorageService {
    private final String connectionString;
    private final String blobContainerName;

    public BlobStorageService(String connectionString, String blobContainerName) {
        this.connectionString = connectionString;
        this.blobContainerName = blobContainerName;
    }

    public BinaryData download(String blobName) {
        BlobServiceClient serviceClient = new BlobServiceClientBuilder()
                .connectionString(connectionString)
                .buildClient();
        BlobContainerClient containerClient = serviceClient.getBlobContainerClient(blobContainerName);
        BlobClient client = containerClient.getBlobClient(blobName);
        return client.downloadContent();
    }
}
