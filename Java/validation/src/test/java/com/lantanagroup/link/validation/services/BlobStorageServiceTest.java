package com.lantanagroup.link.validation.services;

import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.specialized.AppendBlobClient;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoMoreInteractions;
import static org.mockito.Mockito.when;

/**
 * Covers {@link BlobStorageService#appendResource}. Everything is mocked, so no Azure client is built
 * and no network call is made - the service is constructed from an injected container client rather
 * than a connection string.
 */
@ExtendWith(MockitoExtension.class)
class BlobStorageServiceTest {

    private static final String BLOB_NAME = "report-1/patient-abc.ndjson";

    @Mock
    private BlobContainerClient containerClient;
    @Mock
    private BlobClient blobClient;
    @Mock
    private AppendBlobClient appendBlobClient;

    private BlobStorageService service;

    @BeforeEach
    void setUp() {
        service = new BlobStorageService(containerClient);
    }

    private void stubAppendBlobClient() {
        when(containerClient.getBlobClient(BLOB_NAME)).thenReturn(blobClient);
        when(blobClient.getAppendBlobClient()).thenReturn(appendBlobClient);
    }

    private byte[] captureAppendedBytes() throws IOException {
        ArgumentCaptor<InputStream> data = ArgumentCaptor.forClass(InputStream.class);
        ArgumentCaptor<Long> length = ArgumentCaptor.forClass(Long.class);
        verify(appendBlobClient).appendBlock(data.capture(), length.capture());

        byte[] appended = data.getValue().readAllBytes();
        assertEquals(appended.length, length.getValue(),
                "Declared block length must match the bytes actually supplied");
        return appended;
    }

    @Test
    void appendResource_appendsTheLineTerminatedByANewline() throws Exception {
        stubAppendBlobClient();
        String line = "{\"resourceType\":\"OperationOutcome\"}";

        service.appendResource(BLOB_NAME, line);

        assertArrayEquals((line + "\n").getBytes(StandardCharsets.UTF_8), captureAppendedBytes());
    }

    @Test
    void appendResource_encodesAsUtf8() throws Exception {
        // A multi-byte character would break a length computed from String.length() rather than from the
        // encoded bytes, so this pins both the charset and the declared block length.
        stubAppendBlobClient();
        String line = "{\"text\":\"naïve – é\"}";

        service.appendResource(BLOB_NAME, line);

        byte[] expected = (line + "\n").getBytes(StandardCharsets.UTF_8);
        assertArrayEquals(expected, captureAppendedBytes());
    }

    @Test
    void appendResource_targetsTheNamedBlobAsAnAppendBlob() {
        stubAppendBlobClient();

        service.appendResource(BLOB_NAME, "{}");

        verify(containerClient).getBlobClient(BLOB_NAME);
        verify(blobClient).getAppendBlobClient();
    }

    @Test
    void appendResource_neverCreatesTheBlob() {
        // The append blob is created upstream by the Report service's aggregation. Creating it here would
        // truncate the patient NDJSON. verifyNoMoreInteractions covers every create* overload without
        // having to enumerate them.
        stubAppendBlobClient();

        service.appendResource(BLOB_NAME, "{}");

        verify(appendBlobClient).appendBlock(any(InputStream.class), anyLong());
        verifyNoMoreInteractions(appendBlobClient);
    }
}
