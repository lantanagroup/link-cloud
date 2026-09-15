package com.lantanagroup.link.measureeval.services;

import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.models.BlobErrorCode;
import com.azure.storage.blob.models.BlobStorageException;
import com.lantanagroup.link.measureeval.entities.Resource;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.*;

/**
 * Guards the read path's handling of a storage outage vs. a genuinely empty cache. A missing blob
 * (404 BLOB_NOT_FOUND) must read as an empty list, but any other storage failure — or a connectivity
 * failure — must propagate so the async consumer routes the record to -Retry/-Error rather than
 * silently producing a not-reportable report and acking.
 */
class AbsResourceServiceTest {

    private BlobContainerClient containerClient;
    private BlobClient blobClient;
    private AbsResourceService service;

    @BeforeEach
    void setUp() {
        containerClient = mock(BlobContainerClient.class);
        blobClient = mock(BlobClient.class);
        when(containerClient.getBlobClient(anyString())).thenReturn(blobClient);
        service = new AbsResourceService(containerClient, "cache");
    }

    @Test
    void readResources_returnsEmpty_whenBlobNotFound() {
        BlobStorageException notFound = mock(BlobStorageException.class);
        when(notFound.getErrorCode()).thenReturn(BlobErrorCode.BLOB_NOT_FOUND);
        when(blobClient.downloadContent()).thenThrow(notFound);

        List<Resource> resources = service.readResources("fac", "corr", "pat", "corr");

        assertTrue(resources.isEmpty(), "a genuinely missing blob should read as an empty cache");
    }

    @Test
    void readResources_returnsEmpty_whenBlobIsEmpty() {
        when(blobClient.downloadContent()).thenReturn(BinaryData.fromBytes(new byte[0]));

        List<Resource> resources = service.readResources("fac", "corr", "pat", "corr");

        assertTrue(resources.isEmpty());
    }

    @Test
    void readResources_propagates_whenStorageError() {
        BlobStorageException serverError = mock(BlobStorageException.class);
        when(serverError.getErrorCode()).thenReturn(BlobErrorCode.INTERNAL_ERROR);
        when(blobClient.downloadContent()).thenThrow(serverError);

        assertThrows(BlobStorageException.class,
                () -> service.readResources("fac", "corr", "pat", "corr"),
                "a storage outage must propagate so the record is retried, not be swallowed as empty");
    }

    @Test
    void readResources_propagates_whenConnectivityFailure() {
        // Connectivity/timeout failures are not BlobStorageException; they must still propagate.
        when(blobClient.downloadContent()).thenThrow(new RuntimeException("connection refused"));

        assertThrows(RuntimeException.class,
                () -> service.readResources("fac", "corr", "pat", "corr"));
    }

    @Test
    void readResources_parsesResources_whenBlobPresent() {
        String content = "Patient/p1\n{\"resourceType\":\"Patient\",\"id\":\"p1\"}\n";
        when(blobClient.downloadContent())
                .thenReturn(BinaryData.fromBytes(content.getBytes(StandardCharsets.UTF_8)));

        List<Resource> resources = service.readResources("fac", "corr", "pat", "corr");

        assertEquals(1, resources.size());
        assertEquals("p1", resources.get(0).getResourceId());
    }
}
