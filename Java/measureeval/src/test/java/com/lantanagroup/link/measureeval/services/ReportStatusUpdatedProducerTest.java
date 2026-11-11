package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.configs.KafkaConfig;
import com.lantanagroup.link.measureeval.entities.*;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
import com.lantanagroup.link.measureeval.records.EvaluationRequested;
import com.lantanagroup.link.measureeval.records.ResourcesNormalized;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.shared.config.KafkaRetryConfig;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.shared.kafka.records.ResourceKey;
import com.lantanagroup.link.shared.kafka.records.reportstatus.*;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.Node;
import org.apache.kafka.common.PartitionInfo;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.retrytopic.RetryTopicHeaders;

import java.nio.ByteBuffer;
import java.time.ZoneOffset;
import java.util.List;
import java.util.Optional;
import java.util.concurrent.CompletableFuture;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@SuppressWarnings({"unchecked", "rawtypes"})
class ReportStatusUpdatedProducerTest {
    private KafkaTemplate<ReportStatusUpdatedKey, ReportStatusUpdated> template;
    private ReportStatusUpdatedProducer producer;
    private PatientReportingEvaluationStatusRepository patientStatusRepository;

    @BeforeEach
    void setUp() {
        template = mock(KafkaTemplate.class);
        patientStatusRepository = mock(PatientReportingEvaluationStatusRepository.class);
        when(template.send(any(ProducerRecord.class))).thenReturn(CompletableFuture.completedFuture(null));
        producer = new ReportStatusUpdatedProducer(template, patientStatusRepository);
    }

    @Test
    void initialAggregatesMeasuresPerReportAndUsesCanonicalKeyAndHeaders() {
        var patient = patient();
        patient.setReports(List.of(report("report-1", false), report("report-1", true), report("report-2", false)));
        var headers = new RecordHeaders().add(Headers.METRICS_MODE, Headers.getBytes("performance"));
        producer.publishSuccess(patient, QueryType.INITIAL, 42, headers);

        var records = records(2);
        var first = records.get(0);
        assertEquals(Topics.REPORT_STATUS_UPDATED, first.topic());
        assertNull(first.partition());
        assertEquals(new ReportStatusUpdatedKey("report-1", "patient-1"), first.key());
        assertTrue(first.value().isSuccess());
        assertEquals(PatientReportingStatus.INITIAL_EVALUATION, first.value().getStatus());
        assertTrue(first.value().getReportable());
        assertFalse(records.get(1).value().getReportable());
        assertEquals(42L, first.value().getProcessingTime());
        assertEquals(ZoneOffset.UTC, first.value().getTimestamp().getOffset());
        assertEquals("correlation-1", Headers.getCorrelationId(first.headers()));
        assertEquals("performance", Headers.getMetricsMode(first.headers()));
        assertNull(first.value().getValidationResult());
        assertNull(first.value().getErrorMessage());
    }

    @Test
    void supplementalSkipsReportsThatWereNotReportable() {
        var patient = patient();
        patient.setReports(List.of(report("report-1", true), report("report-1", false), report("report-2", false)));
        producer.publishSuccess(patient, QueryType.SUPPLEMENTAL, 0, new RecordHeaders());
        var event = records(1).get(0).value();
        assertEquals("report-1", event.getReportScheduleId());
        assertEquals(PatientReportingStatus.SUPPLEMENTAL_EVALUATION, event.getStatus());
        assertNull(event.getReportable());
        assertEquals(0L, event.getProcessingTime());
    }

    @Test
    void failedStatusPublicationPropagatesAndRetryPreservesOccurrenceAndDuration() {
        when(template.send(any(ProducerRecord.class)))
                .thenReturn(CompletableFuture.failedFuture(new IllegalStateException("broker unavailable")))
                .thenReturn(CompletableFuture.completedFuture(null));
        var headers = new RecordHeaders();
        assertThrows(RuntimeException.class, () -> producer.publishSuccess(patient(), QueryType.INITIAL, 12, headers));
        producer.publishSuccess(patient(), QueryType.INITIAL, 900, headers);
        var sent = records(2);
        assertEquals(sent.get(0).value(), sent.get(1).value());
    }

    @Test
    void finalFailureUsesPhaseAndDistinctSchedulesAndIncludesExceptionMessage() {
        var record = resourceRecord(QueryType.SUPPLEMENTAL);
        when(patientStatusRepository.findByFacilityIdAndCorrelationId("facility", "correlation-1"))
                .thenReturn(Optional.of(patient()));
        producer.noteFailedAttempt(record, 73);
        producer.publishFinalFailure(record, new UnsupportedOperationException("test exception"));
        var event = records(1).get(0).value();
        assertFalse(event.isSuccess());
        assertEquals(PatientReportingStatus.SUPPLEMENTAL_EVALUATION, event.getStatus());
        assertEquals("report-1", event.getReportScheduleId());
        assertEquals("patient-1", event.getPatientId());
        assertEquals("correlation-1", event.getCorrelationId());
        assertEquals(73L, event.getProcessingTime());
        assertEquals("Measure evaluation failed: UnsupportedOperationException: test exception", event.getErrorMessage());
        assertNull(event.getReportable());
    }

    @Test
    void supplementalFinalFailureAggregatesOnlyReportableReports() {
        var patient = patient();
        var unknown = new PatientReportingEvaluationStatus.Report();
        unknown.setReportTrackingId("report-3");
        patient.setReports(List.of(report("report-1", false), report("report-1", true),
                report("report-1", true), report("report-2", false), unknown));
        when(patientStatusRepository.findByFacilityIdAndCorrelationId("facility", "correlation-1"))
                .thenReturn(Optional.of(patient));
        var record = resourceRecord(QueryType.SUPPLEMENTAL);
        var nonreportable = new AbstractResourceRecord.ScheduledReport();
        nonreportable.setReportTrackingId("report-2");
        var unevaluated = new AbstractResourceRecord.ScheduledReport();
        unevaluated.setReportTrackingId("report-3");
        record.value().setScheduledReports(List.of(record.value().getScheduledReports().get(0),
                nonreportable, unevaluated));

        producer.publishFinalFailure(record, new IllegalStateException());

        var event = records(1).get(0).value();
        assertEquals("report-1", event.getReportScheduleId());
        assertEquals(PatientReportingStatus.SUPPLEMENTAL_EVALUATION, event.getStatus());
        assertFalse(event.isSuccess());
        assertNull(event.getReportable());
    }

    @Test
    void supplementalFinalFailureWithoutPatientStatusDoesNotAssumeReportability() {
        producer.publishFinalFailure(resourceRecord(QueryType.SUPPLEMENTAL), new IllegalStateException());
        verifyNoInteractions(template);
    }

    @Test
    void finalFailureSanitizesExceptionMessage() {
        producer.publishFinalFailure(resourceRecord(QueryType.INITIAL),
                new IllegalArgumentException("test\r\nexception\u0000"));
        assertEquals("Measure evaluation failed: IllegalArgumentException: test  exception ",
                records(1).get(0).value().getErrorMessage());
    }

    @org.junit.jupiter.params.ParameterizedTest
    @org.junit.jupiter.params.provider.NullAndEmptySource
    @org.junit.jupiter.params.provider.ValueSource(strings = {"   "})
    void finalFailureWithoutMessageRetainsExceptionType(String message) {
        producer.publishFinalFailure(resourceRecord(QueryType.INITIAL), new IllegalStateException(message));
        assertEquals("Measure evaluation failed: IllegalStateException", records(1).get(0).value().getErrorMessage());
    }

    @Test
    void finalFailureWithoutExceptionUsesFallback() {
        producer.publishFinalFailure(resourceRecord(QueryType.INITIAL), null);
        assertEquals("Measure evaluation failed: Unknown error", records(1).get(0).value().getErrorMessage());
    }

    @Test
    void regenerationFailureUsesNewReportIdAndCorrelationHeader() {
        var value = new EvaluationRequested();
        value.setPatientId("patient-1");
        value.setReportTrackingId("new-report");
        value.setPreviousReportId("old-report");
        var record = new ConsumerRecord<>(Topics.EVALUATION_REQUESTED, 0, 0, "facility", value);
        record.headers().add(Headers.CORRELATION_ID, Headers.getBytes("new-correlation"));
        producer.publishFinalFailure(record, new IllegalStateException());
        var event = records(1).get(0).value();
        assertEquals("new-report", event.getReportScheduleId());
        assertEquals("new-correlation", event.getCorrelationId());
        assertEquals(PatientReportingStatus.SUPPLEMENTAL_EVALUATION, event.getStatus());
        assertNull(event.getProcessingTime());
    }

    @Test
    void malformedRecordCannotPublishPatientStatus() {
        producer.publishFinalFailure(new ConsumerRecord<>("topic", 0, 0, null, new byte[0]), new RuntimeException());
        var record = resourceRecord(QueryType.INITIAL);
        record.key().setPatientId(null);
        producer.publishFinalFailure(record, new RuntimeException());
        verifyNoInteractions(template);
    }

    @Test
    void recovererReportsOnlyTerminalFailuresAndStillCleansCacheIfStatusSendFails() {
        var retryTemplate = (KafkaTemplate<String, ResourcesNormalized>) mock(KafkaTemplate.class);
        when(retryTemplate.partitionsFor(anyString())).thenReturn(
                List.of(new PartitionInfo("topic", 0, null, new Node[0], new Node[0])));
        when(retryTemplate.send(any(ProducerRecord.class))).thenReturn(CompletableFuture.completedFuture(null));
        var config = new KafkaRetryConfig();
        var cleanup = mock(ResourceCacheCleanup.class);
        var recoverer = new KafkaConfig().resourceNormalizedRecoverer(retryTemplate, config, cleanup, producer);
        var record = resourceRecord(QueryType.INITIAL);
        recoverer.accept(record, new RuntimeException("temporary"));
        verifyNoInteractions(template, cleanup);

        record.headers().remove(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS);
        record.headers().add(RetryTopicHeaders.DEFAULT_HEADER_ATTEMPTS, ByteBuffer.allocate(4).putInt(2).array());
        when(template.send(any(ProducerRecord.class)))
                .thenReturn(CompletableFuture.failedFuture(new RuntimeException("offline")));
        assertDoesNotThrow(() -> recoverer.accept(record, new RuntimeException("terminal")));
        assertFalse(records(1).get(0).value().isSuccess());
        verify(cleanup).cleanup("correlation-1", CacheType.REDIS);
    }

    private List<ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated>> records(int count) {
        ArgumentCaptor<ProducerRecord<ReportStatusUpdatedKey, ReportStatusUpdated>> captor = ArgumentCaptor.forClass(ProducerRecord.class);
        verify(template, times(count)).send(captor.capture());
        return captor.getAllValues();
    }

    private static PatientReportingEvaluationStatus patient() {
        var patient = new PatientReportingEvaluationStatus();
        patient.setPatientId("patient-1");
        patient.setCorrelationId("correlation-1");
        patient.setReports(List.of(report("report-1", true)));
        return patient;
    }

    private static PatientReportingEvaluationStatus.Report report(String id, boolean reportable) {
        var report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId(id);
        report.setReportable(reportable);
        return report;
    }

    private static ConsumerRecord<ResourceKey, ResourcesNormalized> resourceRecord(QueryType phase) {
        var value = new ResourcesNormalized();
        value.setQueryType(phase);
        value.setCacheKey("correlation-1");
        value.setCacheType(CacheType.REDIS);
        var scheduled = new AbstractResourceRecord.ScheduledReport();
        scheduled.setReportTrackingId("report-1");
        value.setScheduledReports(List.of(scheduled, scheduled));
        return new ConsumerRecord<>(Topics.RESOURCES_NORMALIZED, 0, 0,
                ResourceKey.builder().facilityId("facility").patientId("patient-1").build(), value);
    }
}
