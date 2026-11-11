package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.records.MeasureReportGenerated;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.junit.jupiter.api.Test;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.support.SendResult;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@SuppressWarnings("unchecked")
class MeasureReportGeneratedProducerTest {
    @Test
    void waitsForBrokerAcknowledgementAndPropagatesAsynchronousFailure() throws Exception {
        KafkaTemplate<String, MeasureReportGenerated> template = mock(KafkaTemplate.class);
        var acknowledgement = new CompletableFuture<SendResult<String, MeasureReportGenerated>>();
        when(template.send(any(ProducerRecord.class))).thenReturn(acknowledgement);
        var producer = new MeasureReportGeneratedProducer(template);
        var patient = new PatientReportingEvaluationStatus();
        patient.setCorrelationId("correlation-1");
        patient.setFacilityId("facility-1");
        patient.setPatientId("patient-1");
        var report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId("report-1");
        report.setReportable(true);
        var publishing = CompletableFuture.runAsync(() ->
                producer.produceMeasureReportGeneratedRecord(patient, report, "measure-report", null, null));
        try {
            verify(template, timeout(2000)).send(any(ProducerRecord.class));
            assertFalse(publishing.isDone());
            acknowledgement.completeExceptionally(new IllegalStateException("broker unavailable"));
            var error = assertThrows(java.util.concurrent.ExecutionException.class,
                    () -> publishing.get(5, TimeUnit.SECONDS));
            assertEquals("Failed to produce MeasureReportGenerated message", error.getCause().getMessage());
        } finally {
            acknowledgement.complete(null);
        }
    }
}
