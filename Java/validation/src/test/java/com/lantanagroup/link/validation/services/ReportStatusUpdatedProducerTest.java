package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.MapperFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.shared.kafka.records.reportstatus.ReportStatusUpdated;
import com.lantanagroup.link.shared.kafka.records.reportstatus.ReportStatusUpdatedKey;
import com.lantanagroup.link.validation.configs.KafkaConfig;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.apache.kafka.common.serialization.Serializer;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.http.converter.json.Jackson2ObjectMapperBuilder;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.support.SendResult;

import java.nio.charset.StandardCharsets;
import java.time.Clock;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class ReportStatusUpdatedProducerTest {
    private static final OffsetDateTime OCCURRED = OffsetDateTime.parse("2026-10-28T13:30:00Z");
    private static final String REPORT_ID = "0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803";
    private static final String PATIENT_ID = "patient-123";
    private static final String CORRELATION_ID = "684e43c1-415b-4d85-9a55-cc9905ddcb63";

    private final ObjectMapper mapper = Jackson2ObjectMapperBuilder.json()
            .featuresToEnable(MapperFeature.ACCEPT_CASE_INSENSITIVE_PROPERTIES,
                    MapperFeature.ACCEPT_CASE_INSENSITIVE_ENUMS)
            .featuresToDisable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS)
            .build();

    @SuppressWarnings("unchecked")
    private final KafkaTemplate<ReportStatusUpdatedKey, ReportStatusUpdated> template = mock(KafkaTemplate.class);

    private ReportStatusUpdatedProducer producer;

    @BeforeEach
    void setUp() throws Exception {
        producer = new ReportStatusUpdatedProducer(
                template,
                Clock.fixed(Instant.parse("2026-10-28T13:30:00Z"), ZoneOffset.UTC));
        CompletableFuture<SendResult<ReportStatusUpdatedKey, ReportStatusUpdated>> future = succeededFuture();
        when(template.send(any(ProducerRecord.class))).thenReturn(future);
    }

    @Test
    void publishSuccess_passedTrue_serializesCanonicalKeyAndPayload() throws Exception {
        ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated> record = captureSuccess(true, 0L);

        assertEquals(Topics.REPORT_STATUS_UPDATED, record.topic());
        assertNull(record.partition());
        assertArrayEquals(canonicalKeyBytes(), keyBytes(record.key()));
        JsonNode value = valueNode(record.value());
        assertEquals(REPORT_ID, value.get("reportScheduleId").asText());
        assertEquals(PATIENT_ID, value.get("patientId").asText());
        assertEquals("Validation", value.get("status").asText());
        assertTrue(value.get("isSuccess").asBoolean());
        assertEquals(125L, value.get("processingTime").asLong());
        assertEquals(CORRELATION_ID, value.get("correlationId").asText());
        assertEquals("2026-10-28T13:30:00Z", value.get("timestamp").asText());
        assertTrue(value.get("validationResult").get("passed").asBoolean());
        assertEquals(0L, value.get("validationResult").get("messageCount").asLong());
        assertFalse(value.has("errorMessage"));
        assertFalse(value.has("reportable"));
        assertEquals(CORRELATION_ID, Headers.getString(record.headers().lastHeader(Headers.CORRELATION_ID).value()));
    }

    @Test
    void publishSuccess_passedFalse_keepsExplicitFalseAndFullMessageCount() throws Exception {
        JsonNode value = valueNode(captureSuccess(false, 4_000_000_000L).value());

        assertTrue(value.get("isSuccess").asBoolean());
        assertFalse(value.get("validationResult").get("passed").asBoolean());
        assertEquals(4_000_000_000L, value.get("validationResult").get("messageCount").asLong());
        assertFalse(value.has("errorMessage"));
    }

    @Test
    void publishFailure_omitsValidationResultAndCarriesErrorMessage() throws Exception {
        producer.publishFailure(REPORT_ID, PATIENT_ID, CORRELATION_ID, 40L, "database unavailable", new RecordHeaders());

        ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated> record = captureRecord();
        assertArrayEquals(canonicalKeyBytes(), keyBytes(record.key()));
        JsonNode value = valueNode(record.value());
        assertEquals("Validation", value.get("status").asText());
        assertFalse(value.get("isSuccess").asBoolean());
        assertEquals("database unavailable", value.get("errorMessage").asText());
        assertEquals(40L, value.get("processingTime").asLong());
        assertEquals(OCCURRED, record.value().getTimestamp());
        assertFalse(value.has("validationResult"));
        assertNull(record.value().getValidationResult());
    }

    @Test
    @SuppressWarnings("unchecked")
    void retryingSend_preservesOccurrenceTimestampAndKey() throws Exception {
        CompletableFuture<SendResult<ReportStatusUpdatedKey, ReportStatusUpdated>> failed = mock(CompletableFuture.class);
        when(failed.get()).thenThrow(new ExecutionException(new RuntimeException("broker")));
        CompletableFuture<SendResult<ReportStatusUpdatedKey, ReportStatusUpdated>> succeeded = succeededFuture();
        when(template.send(any(ProducerRecord.class))).thenReturn(failed, succeeded);

        producer.publishSuccess(REPORT_ID, PATIENT_ID, CORRELATION_ID, 10L, true, 1L, new RecordHeaders());

        @SuppressWarnings("rawtypes")
        ArgumentCaptor<ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);
        verify(template, times(2)).send(captor.capture());
        ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated> first = captor.getAllValues().get(0);
        ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated> second = captor.getAllValues().get(1);
        assertSame(first, second);
        assertEquals(OCCURRED, first.value().getTimestamp());
        assertEquals(OCCURRED, second.value().getTimestamp());
        assertArrayEquals(keyBytes(first.key()), keyBytes(second.key()));
    }

    @Test
    @SuppressWarnings("unchecked")
    void retryingSend_secondFailurePropagatesWithoutRebuildingTheEvent() throws Exception {
        CompletableFuture<SendResult<ReportStatusUpdatedKey, ReportStatusUpdated>> failed = mock(CompletableFuture.class);
        when(failed.get()).thenThrow(new ExecutionException(new RuntimeException("broker")));
        when(template.send(any(ProducerRecord.class))).thenReturn(failed);

        RuntimeException thrown = assertThrows(RuntimeException.class, () ->
                producer.publishFailure(REPORT_ID, PATIENT_ID, CORRELATION_ID, 10L, "database unavailable", null));

        assertTrue(thrown.getMessage().contains("Failed to produce ReportStatusUpdated message"));
        @SuppressWarnings("rawtypes")
        ArgumentCaptor<ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);
        verify(template, times(2)).send(captor.capture());
        assertSame(captor.getAllValues().get(0), captor.getAllValues().get(1));
        assertEquals(OCCURRED, captor.getValue().value().getTimestamp());
        assertNull(captor.getValue().value().getValidationResult());
    }

    @Test
    @SuppressWarnings("unchecked")
    void interruptedSend_doesNotRetry() throws Exception {
        CompletableFuture<SendResult<ReportStatusUpdatedKey, ReportStatusUpdated>> interrupted = mock(CompletableFuture.class);
        when(interrupted.get()).thenThrow(new InterruptedException("stopped"));
        when(template.send(any(ProducerRecord.class))).thenReturn(interrupted);
        try {
            assertThrows(RuntimeException.class, () ->
                    producer.publishSuccess(REPORT_ID, PATIENT_ID, CORRELATION_ID, 10L, true, 1L, null));
            verify(template, times(1)).send(any(ProducerRecord.class));
            assertTrue(Thread.currentThread().isInterrupted());
        } finally {
            Thread.interrupted();
        }
    }

    @Test
    void publishFailure_nullCorrelationId_omitsCorrelationHeaderAndField() throws Exception {
        producer.publishFailure(REPORT_ID, PATIENT_ID, null, 10L, "IllegalStateException", null);

        ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated> record = captureRecord();
        assertNull(record.headers().lastHeader(Headers.CORRELATION_ID));
        assertFalse(valueNode(record.value()).has("correlationId"));
        assertEquals("IllegalStateException", record.value().getErrorMessage());
    }

    private ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated> captureSuccess(boolean passed, long messageCount)
            throws Exception {
        producer.publishSuccess(
                REPORT_ID, PATIENT_ID, CORRELATION_ID, 125L, passed, messageCount, new RecordHeaders());
        return captureRecord();
    }

    @SuppressWarnings({"unchecked", "rawtypes"})
    private ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated> captureRecord() {
        ArgumentCaptor<ProducerRecord> captor = ArgumentCaptor.forClass(ProducerRecord.class);
        verify(template).send(captor.capture());
        return captor.getValue();
    }

    private byte[] keyBytes(ReportStatusUpdatedKey key) {
        return serializer(new KafkaConfig().keySerializer(mapper)).serialize(Topics.REPORT_STATUS_UPDATED, key);
    }

    private JsonNode valueNode(ReportStatusUpdated value) throws Exception {
        byte[] bytes = serializer(new KafkaConfig().valueSerializer(mapper))
                .serialize(Topics.REPORT_STATUS_UPDATED, value);
        return mapper.readTree(bytes);
    }

    @SuppressWarnings("unchecked")
    private static Serializer<Object> serializer(Serializer<?> serializer) {
        return (Serializer<Object>) serializer;
    }

    private static byte[] canonicalKeyBytes() {
        return ("{\"reportId\":\"" + REPORT_ID + "\",\"patientId\":\"" + PATIENT_ID + "\"}")
                .getBytes(StandardCharsets.UTF_8);
    }

    @SuppressWarnings("unchecked")
    private static CompletableFuture<SendResult<ReportStatusUpdatedKey, ReportStatusUpdated>> succeededFuture()
            throws Exception {
        CompletableFuture<SendResult<ReportStatusUpdatedKey, ReportStatusUpdated>> future = mock(CompletableFuture.class);
        when(future.get()).thenReturn(null);
        return future;
    }
}
