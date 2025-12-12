package com.lantanagroup.link.measureeval.services;

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

    public void upload(String blobName, String content) {
        BlobClient client = containerClient.getBlobClient(blobName);
        client.upload(BinaryData.fromString(content));
    }
}
