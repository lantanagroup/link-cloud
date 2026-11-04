package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyBoolean;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;

class ValidationStageFailurePublisherTest {
    private static final String REPORT_ID = "0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803";
    private static final String PATIENT_ID = "patient-123";
    private static final String CORRELATION_ID = "684e43c1-415b-4d85-9a55-cc9905ddcb63";

    private ReportStatusUpdatedProducer producer;
    private ValidationStageFailurePublisher publisher;

    @BeforeEach
    void setUp() {
        producer = mock(ReportStatusUpdatedProducer.class);
        publisher = new ValidationStageFailurePublisher(producer);
    }

    @Test
    void finalFailure_publishesExceptionTypeAndNotedProcessingTime() {
        publisher.noteFailedAttempt(80L);

        publisher.publishFinalFailure(readyRecord(true), new IllegalStateException("database unavailable"));

        verify(producer).publishFailure(
                eq(REPORT_ID),
                eq(PATIENT_ID),
                eq(CORRELATION_ID),
                eq(80L),
                eq("IllegalStateException"),
                any());
        verify(producer, never()).publishSuccess(any(), any(), any(), anyLong(), anyBoolean(), anyLong(), any());
    }

    @Test
    void finalFailure_withoutNotedTime_omitsProcessingTime() {
        publisher.publishFinalFailure(readyRecord(true), new IllegalStateException("database unavailable"));

        verify(producer).publishFailure(
                eq(REPORT_ID), eq(PATIENT_ID), eq(CORRELATION_ID), isNull(), eq("IllegalStateException"), any());
    }

    @Test
    void blankCorrelationHeader_usesCorrelationIdFromTheKey() {
        ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> record = readyRecord(false);
        record.headers().add(Headers.CORRELATION_ID, Headers.getBytes("   "));
        record.key().setCorrelationId(CORRELATION_ID);

        publisher.publishFinalFailure(record, new IllegalStateException("database unavailable"));

        verify(producer).publishFailure(
                eq(REPORT_ID), eq(PATIENT_ID), eq(CORRELATION_ID), isNull(), eq("IllegalStateException"), any());
    }

    @Test
    void rawPayload_doesNotPublish() {
        ConsumerRecord<byte[], byte[]> record = new ConsumerRecord<>(
                Topics.READY_FOR_VALIDATION, 0, 0L, new byte[] {1}, new byte[] {2});

        publisher.noteFailedAttempt(15L);
        publisher.publishFinalFailure(record, new IllegalStateException("could not parse"));

        verifyNoInteractions(producer);
    }

    @Test
    void missingPatientId_doesNotPublish() {
        ReadyForValidation value = new ReadyForValidation();
        value.setReportTrackingId(REPORT_ID);
        value.setPatientId("  ");
        ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> record = new ConsumerRecord<>(
                Topics.READY_FOR_VALIDATION, 0, 0L, new ReadyForValidation.Key(), value);

        publisher.publishFinalFailure(record, new IllegalStateException("database unavailable"));

        verifyNoInteractions(producer);
    }

    @Test
    void nullValue_doesNotPublish() {
        publisher.publishFinalFailure(
                new ConsumerRecord<>(Topics.READY_FOR_VALIDATION, 0, 0L, new ReadyForValidation.Key(), null),
                new IllegalStateException("missing"));

        verifyNoInteractions(producer);
    }

    private static ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> readyRecord(boolean withHeader) {
        ReadyForValidation.Key key = new ReadyForValidation.Key();
        key.setFacilityId("facility-1");
        key.setCorrelationId(CORRELATION_ID);
        ReadyForValidation value = new ReadyForValidation();
        value.setPatientId(PATIENT_ID);
        value.setReportTrackingId(REPORT_ID);
        ConsumerRecord<ReadyForValidation.Key, ReadyForValidation> record = new ConsumerRecord<>(
                Topics.READY_FOR_VALIDATION, 0, 0L, key, value);
        if (withHeader) {
            record.headers().add(Headers.CORRELATION_ID, Headers.getBytes(CORRELATION_ID));
        }
        return record;
    }
}
