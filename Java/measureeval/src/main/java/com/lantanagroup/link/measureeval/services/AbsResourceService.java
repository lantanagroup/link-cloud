package com.lantanagroup.link.measureeval.services;

import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.models.BlobItem;
import com.azure.storage.blob.models.ListBlobsOptions;
import com.lantanagroup.link.measureeval.entities.Resource;
import org.hl7.fhir.r4.model.ResourceType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;


public class AbsResourceService {

    private static final Logger logger = LoggerFactory.getLogger(AbsResourceService.class);

    private final BlobContainerClient containerClient;
    private final String blobRoot;

    public AbsResourceService(BlobContainerClient containerClient, String blobRoot) {
        this.containerClient = containerClient;
        this.blobRoot = blobRoot;
    }

    /**
     * Lightweight connectivity probe for health checks: issues a HEAD against the cache container to
     * confirm the storage account is reachable and the container exists. Returns false (rather than
     * throwing) so callers get a simple up/down signal; the caller is responsible for bounding the
     * call since the Azure SDK applies its own retry/backoff when the account is unreachable.
     */
    public boolean isContainerAvailable() {
        return Boolean.TRUE.equals(containerClient.exists());
    }

    public List<Resource> readResources(String facilityId, String correlationId, String patientId, String cacheKey) {
        String blobName = buildPrefix(cacheKey);
        logger.debug("Reading blob '{}' for correlationId='{}'", blobName, correlationId);

        List<Resource> resources = readBlobResources(blobName, facilityId, correlationId, patientId);

        // int beforeDedup = resources.size();
        // resources = new ArrayList<>(resources.stream()
        //         .collect(java.util.LinkedHashMap<String, Resource>::new,
        //                 (map, r) -> map.putIfAbsent(r.getResourceType().name() + "/" + r.getResourceId(), r),
        //                 java.util.LinkedHashMap::putAll)
        //         .values());
        //
        // if (resources.size() < beforeDedup) {
        //     logger.info("Deduplicated ABS resources for correlationId='{}': {} -> {}", correlationId, beforeDedup, resources.size());
        // }

        logger.debug("Read {} total resources from ABS for correlationId='{}'", resources.size(), correlationId);
        return resources;
    }

    private List<Resource> readBlobResources(String blobName, String facilityId, String correlationId, String patientId) {
        List<Resource> resources = new ArrayList<>();
        int parseFailures = 0;

        byte[] contentBytes;
        try {
            BlobClient blobClient = containerClient.getBlobClient(blobName);
            contentBytes = blobClient.downloadContent().toBytes();
        } catch (Exception e) {
            logger.warn("Failed to download blob '{}' for correlationId='{}': {}.",
                    blobName, correlationId, e.getMessage());
            return resources;
        }

        if (contentBytes.length == 0) {
            return resources;
        }

        String content = new String(contentBytes, StandardCharsets.UTF_8);
        String[] lines = content.split("\n");

        for (int i = 0; i < lines.length - 1; i += 2) {
            String referenceLine = lines[i].trim();
            String jsonLine = lines[i + 1].trim();

            if (referenceLine.isEmpty() || jsonLine.isEmpty()) {
                parseFailures++;
                continue;
            }

            int sep = referenceLine.indexOf('/');
            if (sep <= 0 || sep == referenceLine.length() - 1) {
                logger.warn("Malformed reference line '{}' in blob '{}'. Skipping.",
                        referenceLine, blobName);
                parseFailures++;
                continue;
            }
            String resourceTypeName = referenceLine.substring(0, sep);
            String resourceId = referenceLine.substring(sep + 1);

            ResourceType resourceType;
            try {
                resourceType = ResourceType.fromCode(resourceTypeName);
            } catch (Exception e) {
                logger.warn("Unknown FHIR resource type '{}' in blob '{}'. Skipping.",
                        resourceTypeName, blobName);
                parseFailures++;
                continue;
            }

            Resource resource = new Resource();
            resource.setFacilityId(facilityId);
            resource.setCorrelationId(correlationId);
            resource.setPatientId(patientId);
            resource.setResourceType(resourceType);
            resource.setResourceId(resourceId);
            resource.setResource(jsonLine);
            resources.add(resource);
        }

        logger.debug("Read {} resources from blob '{}' (parseFailures={})",
                resources.size(), blobName, parseFailures);
        return resources;
    }

    public void cleanup(String correlationId) {
        String prefix = blobRoot == null || blobRoot.isEmpty()
                ? correlationId
                : blobRoot + "/" + correlationId;
        ListBlobsOptions options = new ListBlobsOptions().setPrefix(prefix);
        int deleted = 0;
        for (BlobItem blobItem : containerClient.listBlobs(options, null)) {
            containerClient.getBlobClient(blobItem.getName()).deleteIfExists();
            deleted++;
        }
        logger.debug("Deleted {} ABS blob(s) for correlationId='{}'", deleted, correlationId);
    }

private String buildPrefix(String cacheKey) {
        if (blobRoot == null || blobRoot.isEmpty()) {
            return cacheKey;
        }
        return blobRoot + "/" + cacheKey;
    }
}
