package com.lantanagroup.link.validation.services;

import com.azure.core.util.BinaryData;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.utils.LogUtils;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

/**
 * Claim check for the findings carried by {@link com.lantanagroup.link.validation.records.ShadowCompareEvent}.
 * One patient's findings can exceed the Kafka message size limit, so the producer stores them as a blob in the
 * internal container and the event carries only the blob name. The shadow consumer loads them and deletes the
 * blob when it is done.
 * <p>
 * The blobs sit under their own prefix, apart from the patient payloads. Submission copies only the payload
 * blobs it is named, so nothing written here can be submitted.
 */
@Service
public class ShadowFindingsStore {
    private static final TypeReference<List<ShadowFindingDto>> FINDINGS_TYPE = new TypeReference<>() {};

    private final Logger _logger = LoggerFactory.getLogger(ShadowFindingsStore.class);
    private final BlobStorageService blobStorageService;
    private final ObjectMapper objectMapper;
    private final String blobPrefix;

    public ShadowFindingsStore(
            Optional<BlobStorageService> blobStorageService,
            ObjectMapper objectMapper,
            @Value("${vaas.bridge.shadow-compare.blob-prefix:shadow-compare/}") String blobPrefix) {
        this.blobStorageService = blobStorageService.orElse(null);
        this.objectMapper = objectMapper;
        this.blobPrefix = blobPrefix.isEmpty() || blobPrefix.endsWith("/") ? blobPrefix : blobPrefix + "/";
    }

    /**
     * Stores the findings and returns the blob name to put on the event, or {@code null} when no blob storage is
     * configured, in which case the caller sends the findings on the event itself.
     * <p>
     * Each call writes a new blob, so a replay, or the same patient validated again, never overwrites a blob
     * that an event already in the topic still points at.
     */
    public String store(String facilityId, String reportId, String patientId, List<ShadowFindingDto> findings) {
        if (blobStorageService == null) {
            return null;
        }
        String blobName = String.format("%s%s/%s/%s/%s.json", blobPrefix, facilityId, reportId, patientId, UUID.randomUUID());
        try {
            blobStorageService.upload(blobName, BinaryData.fromBytes(objectMapper.writeValueAsBytes(findings)));
        } catch (JsonProcessingException e) {
            throw new UncheckedIOException(e);
        }
        return blobName;
    }

    /** Loads the findings stored under {@code blobName}. Throws when the blob does not exist. */
    public List<ShadowFindingDto> load(String blobName) {
        if (blobStorageService == null) {
            throw new IllegalStateException("The event's findings are in blob storage, but no blob storage is configured");
        }
        BinaryData data = blobStorageService.download(blobName);
        try {
            return objectMapper.readValue(data.toBytes(), FINDINGS_TYPE);
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }

    /**
     * Deletes the blob, if there is one. Never throws: a blob left behind costs only storage, and the storage
     * account's lifecycle rule for the prefix removes it later.
     */
    public void delete(String blobName) {
        if (blobName == null || blobStorageService == null) {
            return;
        }
        try {
            blobStorageService.deleteIfExists(blobName);
        } catch (Exception e) {
            _logger.warn("Failed to delete shadow findings blob {}; leaving it for the lifecycle rule",
                    LogUtils.sanitize(blobName), e);
        }
    }
}
