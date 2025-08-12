package com.lantanagroup.link.validation.services;

import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClient;
import com.azure.storage.blob.BlobServiceClientBuilder;

public class BlobStorageService {
    private final BlobContainerClient containerClient;

    public BlobStorageService(String connectionString, String blobContainerName) {
        BlobServiceClient serviceClient = new BlobServiceClientBuilder()
                .connectionString(connectionString)
                .buildClient();
        containerClient = serviceClient.getBlobContainerClient(blobContainerName);
    }

    public BinaryData download(String blobName) {
        BlobClient client = containerClient.getBlobClient(blobName);
        return client.downloadContent();
    }
}
