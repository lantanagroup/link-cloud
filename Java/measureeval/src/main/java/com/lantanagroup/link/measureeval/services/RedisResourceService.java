package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.exceptions.ResourceCacheUnavailableException;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.hl7.fhir.r4.model.ResourceType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.data.redis.core.HashOperations;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;


@Service
public class RedisResourceService {

    private static final Logger logger = LoggerFactory.getLogger(RedisResourceService.class);

    private final StringRedisTemplate redisTemplate;

    public RedisResourceService(StringRedisTemplate redisTemplate) {
        this.redisTemplate = redisTemplate;
    }


    /**
     * Field-name prefix reserved for cache metadata rather than FHIR resources. Resource fields are
     * always {@code resourceType/resourceId}, so the two can never collide. Shared with the .NET
     * writers; see docs-dev/resource-cache.md.
     */
    static final String METADATA_FIELD_PREFIX = "__";

    public List<Resource> readResources(String facilityId, String correlationId, String patientId) {
        return readEntry(facilityId, correlationId, patientId).resources();
    }

    /**
     * Reads the entry's resources and its recorded durable count in one HGETALL. Reading the count
     * separately left a gap: an eviction between the two calls made a partial entry look count-less,
     * so the reader trusted it.
     */
    public RedisCacheEntry readEntry(String facilityId, String correlationId, String patientId) {
        HashOperations<String, String, String> hashOps = redisTemplate.opsForHash();

        Map<String, String> fields;
        try {
            fields = hashOps.entries(correlationId);
        } catch (DataAccessException e) {
            // Differentiate a cache OUTAGE from a genuinely-absent key. A connection/command failure means
            // the resources are unknown — not empty — so surface an explicit, transient (retryable) error
            // instead of letting an empty/partial read masquerade as "no resources" (a bogus not-reportable)
            // or a downstream ResourceNotFoundException. The record is then retried and redelivered once
            // Redis recovers, rather than being evaluated against an incomplete bundle.
            throw new ResourceCacheUnavailableException(
                    "Resource cache (Redis) unavailable while reading resources for correlationId=" + correlationId, e);
        }

        List<Resource> resources = new ArrayList<>();

        if (fields.isEmpty()) {
            // Reached only when the cache was reachable: the key genuinely has no fields.
            logger.debug("No Redis entries for correlationId='{}' (cache reachable, key absent)", LogUtils.sanitize(correlationId));
            return new RedisCacheEntry(resources, null, false);
        }

        int malformedFields = 0;
        int unknownTypes = 0;
        Integer durableCount = null;
        boolean durableCountUnparseable = false;

        for (Map.Entry<String, String> entry : fields.entrySet()) {
            String field = entry.getKey();
            String json = entry.getValue();
            if (json == null || json.isEmpty()) {
                continue;
            }

            // Cache metadata, not a resource. The writers share the entry with fields that describe
            // it -- the durable resource count the .NET side records so a reader can tell an entry
            // recreated by a partial append from a whole one -- and they are deliberately in the same
            // hash so one TTL covers them and deleting the entry clears them with it. Skipped quietly:
            // warning here would fire once per correlation read, on the hottest path there is.
            // See docs-dev/resource-cache.md.
            if (field.startsWith(METADATA_FIELD_PREFIX)) {
                if (DURABLE_RESOURCE_COUNT_FIELD.equals(field)) {
                    try {
                        durableCount = Integer.parseInt(json.trim());
                    } catch (NumberFormatException e) {
                        logger.warn("Unparseable durable resource count '{}' on Redis key '{}'. Treating the count as unrecorded.",
                                LogUtils.sanitize(json), LogUtils.sanitize(correlationId));
                        durableCountUnparseable = true;
                    }
                }
                continue;
            }

            int sep = field.indexOf('/');
            if (sep <= 0 || sep == field.length() - 1) {
                logger.warn("Malformed Redis hash field '{}' in key '{}'. Expected 'resourceType/resourceId'. Skipping.", field, correlationId);
                malformedFields++;
                continue;
            }
            String resourceTypeName = field.substring(0, sep);
            String resourceId = field.substring(sep + 1);

            ResourceType resourceType;
            try {
                resourceType = ResourceType.fromCode(resourceTypeName);
            } catch (Exception e) {
                logger.warn("Unknown FHIR resource type '{}' for field '{}' in key '{}'. Skipping.",
                        resourceTypeName, field, correlationId);
                unknownTypes++;
                continue;
            }

            Resource resource = new Resource();
            resource.setFacilityId(facilityId);
            resource.setCorrelationId(correlationId);
            resource.setPatientId(patientId);
            resource.setResourceType(resourceType);
            resource.setResourceId(resourceId);
            resource.setResource(json);
            resources.add(resource);
        }

        logger.debug("Read {} resources for correlationId='{}' (malformed={}, unknownTypes={}, durableCount={})",
                resources.size(), correlationId, malformedFields, unknownTypes, durableCount);
        return new RedisCacheEntry(resources, durableCount, durableCountUnparseable);
    }

    /**
     * Hash field the .NET durable writer records once a blob write for the entry has landed: the
     * number of resources the durable store holds for it (LEGLINK-1276). It shares the entry's hash
     * so one TTL covers both and deleting the entry clears it. The "__" prefix is reserved for
     * metadata; a resource field is always {@code resourceType/resourceId}, so the two cannot
     * collide. See docs-dev/resource-cache.md.
     */
    static final String DURABLE_RESOURCE_COUNT_FIELD = "__durableResourceCount";

    public void cleanup(String correlationId) {
        // Only {correlationId}, the normalized entry MeasureEval reads. The acquisition keys
        // ({correlationId}:{ResourceType}) belong to Normalization, which deletes them after producing
        // ResourcesNormalized and purges them on every terminal failure; any it fails to delete expire
        // with the TTL. One key per command is also what the OSS clustering policy of our Azure
        // Managed Redis requires: a multi-key UNLINK spanning hash slots is rejected with CROSSSLOT.
        Boolean unlinked = redisTemplate.unlink(correlationId);
        logger.debug("Cleaned up Redis key for correlationId='{}' (present={})",
                LogUtils.sanitize(correlationId), Boolean.TRUE.equals(unlinked));
    }
}
