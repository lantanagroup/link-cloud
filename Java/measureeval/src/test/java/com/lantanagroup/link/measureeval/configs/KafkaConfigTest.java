package com.lantanagroup.link.measureeval.configs;

import ca.uhn.fhir.parser.DataFormatException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.measureeval.exceptions.ResourceCacheUnavailableException;
import com.lantanagroup.link.shared.config.KafkaRetryConfig;
import com.lantanagroup.link.shared.exceptions.FhirParseException;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.kafka.RetryTopicRecoverer;
import com.lantanagroup.link.shared.kafka.RetryTopicRecovererFactory;
import com.lantanagroup.link.shared.kafka.records.ResourceKey;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.Node;
import org.apache.kafka.common.PartitionInfo;
import org.apache.kafka.common.serialization.Serializer;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.dao.DataAccessResourceFailureException;
import org.springframework.data.redis.RedisConnectionFailureException;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.retrytopic.RetryTopicHeaders;
import org.springframework.kafka.support.serializer.DelegatingByTypeSerializer;
import org.springframework.kafka.support.serializer.DeserializationException;
import org.springframework.messaging.MessageHandlingException;
import org.springframework.messaging.support.GenericMessage;

import java.nio.ByteBuffer;
import java.time.Duration;
import java.util.Collections;
import java.util.List;
import java.util.concurrent.CompletableFuture;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.*;

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
    //
    // These exercise the SHARED classifier against the SERVICE'S OWN production poison set
    // (KafkaConfig.NON_RETRYABLE) — not a local copy — so adding/removing a type in production, or a
    // change to the cause-chain walk, is caught here rather than passing against a stale duplicate.

    private static boolean isNonRetryable(Throwable t) {
        return RetryTopicRecovererFactory.isNonRetryable(t, KafkaConfig.NON_RETRYABLE);
    }

    @Test
    void validationException_isNonRetryable() {
        assertTrue(isNonRetryable(new ValidationException("Query Type is null.")));
    }

    @Test
    void dataFormatException_isNonRetryable() {
        assertTrue(isNonRetryable(new DataFormatException("malformed FHIR")));
    }

    @Test
    void fhirParseException_isNonRetryable() {
        assertTrue(isNonRetryable(new FhirParseException(new DataFormatException("bad"))));
    }

    @Test
    void messageHandlingException_isNonRetryable() {
        assertTrue(isNonRetryable(new MessageHandlingException(new GenericMessage<>("payload"))));
    }

    @Test
    void deserializationException_isNonRetryable() {
        assertTrue(isNonRetryable(
                new DeserializationException("bad", new byte[0], false, new RuntimeException("x"))));
    }

    @Test
    void nestedDataFormatException_isNonRetryable() {
        // Mirrors the observed kafka_dlt-exception-cause-fqcn: DataFormatException nested as a cause.
        Throwable nested = new RuntimeException("wrapper",
                new IllegalStateException("middle", new DataFormatException("bad fhir")));
        assertTrue(isNonRetryable(nested));
    }

    @Test
    void mongoLikeFailure_isRetryable() {
        // A transient infra outage (e.g. Mongo down) must NOT be treated as poison.
        assertFalse(isNonRetryable(new DataAccessResourceFailureException("mongo unavailable")));
    }

    @Test
    void genericRuntimeException_isRetryable() {
        assertFalse(isNonRetryable(new RuntimeException("transient boom")));
    }

    @Test
    void redisDown_isRetryable() {
        // Redis outage during a cache read is transient — must be retried, not dead-lettered.
        assertFalse(isNonRetryable(new RedisConnectionFailureException("redis unavailable")));
    }

    @Test
    void resourceCacheUnavailable_isRetryable() {
        // The explicit cache-outage error RedisResourceService now throws must also be retried, not poison.
        assertFalse(isNonRetryable(
                new ResourceCacheUnavailableException("cache down", new RedisConnectionFailureException("x"))));
    }

    @Test
    void nullThrowable_isRetryable() {
        assertFalse(isNonRetryable(null));
    }

    // ---- Destination resolver: drive the REAL RetryTopicRecovererFactory.create() wiring and assert
    //      which topic a failed record is routed to. Covers every resolver branch (disable / poison /
    //      exhausted -> -Error; transient with attempts remaining -> -Retry), using the production set.

    private static final String RETRY_TOPIC = "ResourcesNormalized-Retry";
    private static final String ERROR_TOPIC = "ResourcesNormalized-Error";

    private static ConsumerRecord<String, String> record() {
        return new ConsumerRecord<>("ResourcesNormalized", 0, 0L, "key", "value");
    }

    /** Pre-stamp the attempts header as if {@code attempts} retries already occurred. */
    private static void stampAttempts(ConsumerRecord<?, ?> record, int attempts) {
        record.headers().add(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS,
                ByteBuffer.allocate(Integer.BYTES).putInt(attempts).array());
    }

    private static KafkaRetryConfig retryConfig(int maxAttempts, boolean disableRetryConsumer) {
        KafkaRetryConfig config = new KafkaRetryConfig();
        // effectiveMaxAttempts() == size + 1, so (maxAttempts - 1) delays yields exactly maxAttempts attempts.
        config.setConsumerRetryDuration(Collections.nCopies(Math.max(maxAttempts - 1, 0), Duration.ofSeconds(1)));
        config.setDisableRetryConsumer(disableRetryConsumer);
        return config;
    }

    /**
     * Builds the recoverer exactly as production does — {@link RetryTopicRecovererFactory#create} with
     * {@link KafkaConfig#NON_RETRYABLE} — drives a failed record through it, and returns the topic the
     * resolver published to. The {@link RetryTopicRecoverer} increments the attempts header before the
     * delegate resolves, so the resolver sees the post-increment count, mirroring runtime.
     */
    @SuppressWarnings("unchecked")
    private static String routedTopic(KafkaRetryConfig config, ConsumerRecord<String, String> record, Exception exception) {
        KafkaTemplate<Object, Object> template = mock(KafkaTemplate.class);
        when(template.partitionsFor(anyString())).thenAnswer(invocation ->
                List.of(new PartitionInfo(invocation.getArgument(0), 0, null, new Node[0], new Node[0])));
        when(template.send(any(ProducerRecord.class)))
                .thenReturn(CompletableFuture.completedFuture(null));

        RetryTopicRecoverer recoverer = RetryTopicRecovererFactory.create(
                template, RETRY_TOPIC, ERROR_TOPIC, config, KafkaConfig.NON_RETRYABLE);
        recoverer.accept(record, exception);

        ArgumentCaptor<ProducerRecord<Object, Object>> captor = ArgumentCaptor.forClass(ProducerRecord.class);
        verify(template).send(captor.capture());
        return captor.getValue().topic();
    }

    @Test
    void transientFailure_withAttemptsRemaining_routesToRetry() {
        assertEquals(RETRY_TOPIC,
                routedTopic(retryConfig(3, false), record(), new RuntimeException("transient boom")));
    }

    @Test
    void redisDown_withAttemptsRemaining_routesToRetry() {
        // A Redis outage must land in -Retry (transient), not -Error.
        assertEquals(RETRY_TOPIC,
                routedTopic(retryConfig(3, false), record(), new RedisConnectionFailureException("redis unavailable")));
    }

    @Test
    void poisonException_routesToError() {
        assertEquals(ERROR_TOPIC,
                routedTopic(retryConfig(3, false), record(), new ValidationException("Query Type is null.")));
    }

    @Test
    void nestedPoisonException_routesToError() {
        // Poison buried in the cause chain must still dead-letter, not retry.
        Exception nested = new RuntimeException("wrapper",
                new IllegalStateException("middle", new DataFormatException("bad fhir")));
        assertEquals(ERROR_TOPIC, routedTopic(retryConfig(3, false), record(), nested));
    }

    @Test
    void disableRetryConsumer_routesToError_evenForTransient() {
        // disable-retry-consumer short-circuits everything straight to -Error.
        assertEquals(ERROR_TOPIC,
                routedTopic(retryConfig(3, true), record(), new RuntimeException("transient boom")));
    }

    @Test
    void exhaustedAttempts_routesToError() {
        // effectiveMaxAttempts = 3 (flat); a record already retried twice increments to attempt 3 == max.
        ConsumerRecord<String, String> record = record();
        stampAttempts(record, 2);
        assertEquals(ERROR_TOPIC,
                routedTopic(retryConfig(3, false), record, new RuntimeException("still failing")));
    }

    @Test
    void lastAttemptBeforeExhaustion_routesToRetry() {
        // Boundary: incremented attempt 2 < max 3 must still go to -Retry, not -Error.
        ConsumerRecord<String, String> record = record();
        stampAttempts(record, 1);
        assertEquals(RETRY_TOPIC,
                routedTopic(retryConfig(3, false), record, new RuntimeException("still failing")));
    }
}
