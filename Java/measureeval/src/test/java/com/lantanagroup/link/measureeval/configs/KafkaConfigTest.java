package com.lantanagroup.link.measureeval.configs;

import ca.uhn.fhir.parser.DataFormatException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.exceptions.FhirParseException;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.kafka.records.ResourceKey;
import org.apache.kafka.common.serialization.Serializer;
import org.junit.jupiter.api.Test;
import org.springframework.dao.DataAccessResourceFailureException;
import org.springframework.kafka.support.serializer.DelegatingByTypeSerializer;
import org.springframework.kafka.support.serializer.DeserializationException;
import org.springframework.messaging.MessageHandlingException;
import org.springframework.messaging.support.GenericMessage;

import java.lang.reflect.Method;
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

    // ---- isNonRetryable: poison classification used by the retry destination resolver ------------

    private static boolean isNonRetryable(Throwable t) throws Exception {
        Method m = KafkaConfig.class.getDeclaredMethod("isNonRetryable", Throwable.class);
        m.setAccessible(true);
        return (boolean) m.invoke(null, t);
    }

    @Test
    void validationException_isNonRetryable() throws Exception {
        assertTrue(isNonRetryable(new ValidationException("Query Type is null.")));
    }

    @Test
    void dataFormatException_isNonRetryable() throws Exception {
        assertTrue(isNonRetryable(new DataFormatException("malformed FHIR")));
    }

    @Test
    void fhirParseException_isNonRetryable() throws Exception {
        assertTrue(isNonRetryable(new FhirParseException(new DataFormatException("bad"))));
    }

    @Test
    void messageHandlingException_isNonRetryable() throws Exception {
        assertTrue(isNonRetryable(new MessageHandlingException(new GenericMessage<>("payload"))));
    }

    @Test
    void deserializationException_isNonRetryable() throws Exception {
        assertTrue(isNonRetryable(
                new DeserializationException("bad", new byte[0], false, new RuntimeException("x"))));
    }

    @Test
    void nestedDataFormatException_isNonRetryable() throws Exception {
        // Mirrors the observed kafka_dlt-exception-cause-fqcn: DataFormatException nested as a cause.
        Throwable nested = new RuntimeException("wrapper",
                new IllegalStateException("middle", new DataFormatException("bad fhir")));
        assertTrue(isNonRetryable(nested));
    }

    @Test
    void mongoLikeFailure_isRetryable() throws Exception {
        // A transient infra outage (e.g. Mongo down) must NOT be treated as poison.
        assertFalse(isNonRetryable(new DataAccessResourceFailureException("mongo unavailable")));
    }

    @Test
    void genericRuntimeException_isRetryable() throws Exception {
        assertFalse(isNonRetryable(new RuntimeException("transient boom")));
    }

    @Test
    void nullThrowable_isRetryable() throws Exception {
        assertFalse(isNonRetryable(null));
    }
}
