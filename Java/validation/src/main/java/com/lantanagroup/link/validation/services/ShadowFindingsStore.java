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
import java.util.regex.Pattern;

/**
 * Claim check for the findings carried by {@link com.lantanagroup.link.validation.records.ShadowCompareEvent}.
 * One patient's findings can exceed the Kafka message size limit, so the producer stores them as a blob in the
 * internal container and the event carries only the blob name. The shadow consumer loads them and deletes the
 * blob when it is done.
 * <p>
 * The blobs sit under their own prefix, apart from the patient payloads. Submission copies only the payload
 * blobs it is named, so nothing written here can be submitted.
 * <p>
 * The blob name on an event is not trusted. This store shares its container with the patient payloads, so
 * {@link #load} and {@link #delete} act only on a name of the exact shape {@link #store} produces, under the
 * configured prefix. Anything else is refused, so an event cannot be used to read or delete another blob.
 * <p>
 * That includes a blob under a previous prefix. An event queued before the prefix changed is skipped and its blob
 * left in place, so the topic must drain before the prefix changes; the {@code app-config.yaml} entry says so.
 */
@Service
public class ShadowFindingsStore {
    private static final TypeReference<List<ShadowFindingDto>> FINDINGS_TYPE = new TypeReference<>() {};
    private static final String DEFAULT_BLOB_PREFIX = "shadow-compare/";
    /** One path segment of a blob name: no slash and no dot, so it can neither add a level nor climb one. */
    private static final String SEGMENT = "[A-Za-z0-9_-]+";
    private static final Pattern UNSAFE_SEGMENT_CHARACTER = Pattern.compile("[^A-Za-z0-9_-]");
    private static final String UUID_PATTERN = "[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}";

    private final Logger _logger = LoggerFactory.getLogger(ShadowFindingsStore.class);
    private final BlobStorageService blobStorageService;
    private final ObjectMapper objectMapper;
    private final String blobPrefix;
    private final Pattern ownBlobName;

    public ShadowFindingsStore(
            Optional<BlobStorageService> blobStorageService,
            ObjectMapper objectMapper,
            @Value("${vaas.bridge.shadow-compare.blob-prefix:shadow-compare/}") String blobPrefix) {
        this.blobStorageService = blobStorageService.orElse(null);
        this.objectMapper = objectMapper;
        if (blobPrefix == null || blobPrefix.isBlank()) {
            // Without a prefix the blobs would sit at the container root, where no name check could tell them
            // from anything else in the container.
            _logger.warn("vaas.bridge.shadow-compare.blob-prefix is empty; using {}", DEFAULT_BLOB_PREFIX);
            blobPrefix = DEFAULT_BLOB_PREFIX;
        }
        this.blobPrefix = blobPrefix.endsWith("/") ? blobPrefix : blobPrefix + "/";
        this.ownBlobName = Pattern.compile(Pattern.quote(this.blobPrefix)
                + SEGMENT + "/" + SEGMENT + "/" + SEGMENT + "/" + UUID_PATTERN + "\\.json");
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
        // The ids come from the Kafka record. They are in the name only to make a blob traceable, so any
        // character that could change the path is replaced.
        String blobName = String.format("%s%s/%s/%s/%s.json", blobPrefix,
                segment(facilityId), segment(reportId), segment(patientId), UUID.randomUUID());
        try {
            blobStorageService.upload(blobName, BinaryData.fromBytes(objectMapper.writeValueAsBytes(findings)));
        } catch (JsonProcessingException e) {
            throw new UncheckedIOException(e);
        }
        return blobName;
    }

    /**
     * Loads the findings stored under {@code blobName}. Throws when the blob does not exist, or when the name is
     * not one {@link #store} produces.
     */
    public List<ShadowFindingDto> load(String blobName) {
        if (blobStorageService == null) {
            throw new IllegalStateException("The event's findings are in blob storage, but no blob storage is configured");
        }
        if (!isOwnBlobName(blobName)) {
            throw new IllegalArgumentException("Not a shadow findings blob name: " + LogUtils.sanitize(blobName));
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
     * account's lifecycle rule for the prefix removes it later. A name that is not one {@link #store} produces
     * is refused and nothing is deleted.
     */
    public void delete(String blobName) {
        if (blobName == null || blobStorageService == null) {
            return;
        }
        if (!isOwnBlobName(blobName)) {
            _logger.warn("Refusing to delete {}: not a shadow findings blob name", LogUtils.sanitize(blobName));
            return;
        }
        try {
            blobStorageService.deleteIfExists(blobName);
        } catch (Exception e) {
            _logger.warn("Failed to delete shadow findings blob {}; leaving it for the lifecycle rule",
                    LogUtils.sanitize(blobName), e);
        }
    }

    private boolean isOwnBlobName(String blobName) {
        return blobName != null && ownBlobName.matcher(blobName).matches();
    }

    private static String segment(String id) {
        if (id == null || id.isEmpty()) {
            return "_";
        }
        return UNSAFE_SEGMENT_CHARACTER.matcher(id).replaceAll("_");
    }
}
