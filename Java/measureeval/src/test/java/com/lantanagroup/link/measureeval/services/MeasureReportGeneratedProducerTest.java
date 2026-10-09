package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.records.MeasureReportGenerated;
import com.lantanagroup.link.shared.kafka.KafkaKeys;
import com.lantanagroup.link.shared.kafka.Topics;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.kafka.core.KafkaTemplate;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;

class MeasureReportGeneratedProducerTest {

    @Test
    @SuppressWarnings("unchecked")
    void produce_keysByPatientAndKeepsIdsOnTheValue() {
        KafkaTemplate<String, MeasureReportGenerated> template = mock(KafkaTemplate.class);
        MeasureReportGeneratedProducer producer = new MeasureReportGeneratedProducer(template);

        PatientReportingEvaluationStatus status = new PatientReportingEvaluationStatus();
        status.setFacilityId("facility-1");
        status.setPatientId("patient-9");
        status.setCorrelationId("corr-1");
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId("track-1");
        report.setReportType("TypeA");
        report.setReportable(true);

        ArgumentCaptor<ProducerRecord<String, MeasureReportGenerated>> captor =
                ArgumentCaptor.forClass(ProducerRecord.class);

        producer.produceMeasureReportGeneratedRecord(status, report, "mr-1", "uri", "blob");

        verify(template).send(captor.capture());
        ProducerRecord<String, MeasureReportGenerated> sent = captor.getValue();
        assertEquals(Topics.MEASURE_REPORT_GENERATED, sent.topic());
        assertNull(sent.partition());
        assertEquals(KafkaKeys.forPatient("facility-1", "patient-9"), sent.key());
        assertEquals("facility-1", sent.value().getFacilityId());
        assertEquals("patient-9", sent.value().getPatientId());
        assertEquals("mr-1", sent.value().getMeasureReportId());
    }

    @Test
    @SuppressWarnings("unchecked")
    void produce_rejectsAMissingPatientId() {
        KafkaTemplate<String, MeasureReportGenerated> template = mock(KafkaTemplate.class);
        MeasureReportGeneratedProducer producer = new MeasureReportGeneratedProducer(template);

        PatientReportingEvaluationStatus status = new PatientReportingEvaluationStatus();
        status.setFacilityId("facility-1");
        status.setCorrelationId("corr-1");
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId("track-1");

        assertThrows(IllegalArgumentException.class,
                () -> producer.produceMeasureReportGeneratedRecord(status, report, "mr-1", null, null));
        verify(template, never()).send(any(ProducerRecord.class));
    }
}
