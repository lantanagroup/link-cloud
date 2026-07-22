package com.lantanagroup.link.validation.services;

import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
import com.azure.storage.blob.specialized.AppendBlobClient;

import java.io.ByteArrayInputStream;
import java.nio.charset.StandardCharsets;

public class BlobStorageService {
    private final BlobContainerClient containerClient;

    public BlobStorageService(String connectionString, String blobContainerName) {
        BlobServiceClient serviceClient = new BlobServiceClientBuilder()
                .connectionString(connectionString)
                .buildClient();
        containerClient = serviceClient.getBlobContainerClient(blobContainerName);
    }

    /**
     * Visible for testing: takes an already-built container client so the append behaviour can be
     * exercised against mocks without constructing a real Azure client.
     */
    BlobStorageService(BlobContainerClient containerClient) {
        this.containerClient = containerClient;
    }

    public BinaryData download(String blobName) {
        BlobClient client = containerClient.getBlobClient(blobName);
        return client.downloadContent();
    }

    /**
     * Appends a single NDJSON line (one serialized FHIR resource) to an existing patient NDJSON append
     * blob. The blob is created upstream by the Report service's aggregation as an append blob, so this
     * does not call {@code create()}.
     * <p>
     * A trailing newline is written so the appended resource always terminates its own line. Without it,
     * any subsequent appender (e.g. the Report service's legacy OperationOutcome write, which is only
     * retired once LEGLINK-466's flag is set) concatenates onto this line and produces malformed NDJSON.
     */
    public void appendResource(String blobName, String ndjsonLine) {
        AppendBlobClient client = containerClient.getBlobClient(blobName).getAppendBlobClient();
        byte[] bytes = (ndjsonLine + "\n").getBytes(StandardCharsets.UTF_8);
        client.appendBlock(new ByteArrayInputStream(bytes), bytes.length);
    }
}
