package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.exceptions.ResourceCacheUnavailableException;
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


    public List<Resource> readResources(String facilityId, String correlationId, String patientId) {
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
            logger.debug("No Redis entries for correlationId='{}' (cache reachable, key absent)", correlationId);
            return resources;
        }

        int malformedFields = 0;
        int unknownTypes = 0;

        for (Map.Entry<String, String> entry : fields.entrySet()) {
            String field = entry.getKey();
            String json = entry.getValue();
            if (json == null || json.isEmpty()) {
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

        logger.debug("Read {} resources for correlationId='{}' (malformed={}, unknownTypes={})",
                resources.size(), correlationId, malformedFields, unknownTypes);
        return resources;
    }


    public void cleanup(String correlationId) {
        redisTemplate.unlink(correlationId);
        logger.debug("Cleaned up Redis key '{}'", correlationId);
    }
}
