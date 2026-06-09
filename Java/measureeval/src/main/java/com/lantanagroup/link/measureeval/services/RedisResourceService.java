package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import org.hl7.fhir.r4.model.ResourceType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
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
        Map<String, String> fields = hashOps.entries(correlationId);
        List<Resource> resources = new ArrayList<>();

        if (fields.isEmpty()) {
            logger.debug("No Redis entries for correlationId='{}'", correlationId);
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
