package com.lantanagroup.link.measureeval.configs;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.kafka.records.ResourceKey;
import org.apache.kafka.common.serialization.Serializer;
import org.junit.jupiter.api.Test;
import org.springframework.kafka.support.serializer.DelegatingByTypeSerializer;

import java.util.Map;

import static org.junit.jupiter.api.Assertions.*;

class KafkaConfigTest {

    @Test
    void keySerializer_ShouldHandleResourceKey() {
        KafkaConfig kafkaConfig = new KafkaConfig();
        ObjectMapper objectMapper = new ObjectMapper();
        
        Serializer<?> serializer = kafkaConfig.keySerializer(objectMapper);
        
        assertNotNull(serializer);
        assertTrue(serializer instanceof DelegatingByTypeSerializer);
        
        ResourceKey key = ResourceKey.builder()
                .facilityId("test-facility")
                .patientId("test-correlation")
                .build();
        
        byte[] serialized = ((Serializer<Object>) serializer).serialize("test-topic", key);
        
        assertNotNull(serialized);
        assertTrue(serialized.length > 0);
        
        // Verify it can be deserialized back (sanity check of the JSON format)
        try {
            ResourceKey deserialized = objectMapper.readValue(serialized, ResourceKey.class);
            assertEquals(key.getFacilityId(), deserialized.getFacilityId());
            assertEquals(key.getPatientId(), deserialized.getPatientId());
        } catch (Exception e) {
            fail("Failed to deserialize serialized ResourceKey: " + e.getMessage());
        }
    }

    @Test
    void keySerializer_ShouldHandleString() {
        KafkaConfig kafkaConfig = new KafkaConfig();
        ObjectMapper objectMapper = new ObjectMapper();
        
        Serializer<?> serializer = kafkaConfig.keySerializer(objectMapper);
        
        String key = "test-key";
        byte[] serialized = ((Serializer<Object>) serializer).serialize("test-topic", key);
        
        assertNotNull(serialized);
        assertEquals(key, new String(serialized));
    }
}
