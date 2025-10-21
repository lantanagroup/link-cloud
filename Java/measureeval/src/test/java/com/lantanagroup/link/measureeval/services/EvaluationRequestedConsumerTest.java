package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.EvaluationRequested;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.measureeval.repositories.ResourceRepository;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Patient;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;

import java.util.Collections;
import java.util.Optional;
import java.util.function.Predicate;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

class EvaluationRequestedConsumerTest {

    @Mock
    private ResourceRepository resourceRepository;

    @Mock
    private PatientReportingEvaluationStatusRepository patientStatusRepository;

    @Mock
    private Predicate<MeasureReport> reportabilityPredicate;

    @Mock
    private KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;

    @Mock
    private MeasureEvalMetrics measureEvalMetrics;

    @Mock
    private PatientStatusBundler patientStatusBundler;

    @Mock
    private MeasureReportGeneratedProducer measureReportGeneratedProducer;

    @Mock
    private BlobStorageService blobStorageService;

    @Mock
    private EvaluateMeasureService evaluateMeasureService;

    @Mock
    private ConsumerRecordRecoverer recoverer;

    @Mock
    private FhirContext fhirContext;

    private AutoCloseable mocks;
    private EvaluationRequestedConsumer consumer;

    @BeforeEach
    void setUp() {
        mocks = MockitoAnnotations.openMocks(this);
        consumer = new EvaluationRequestedConsumer(
                resourceRepository,
                patientStatusRepository,
                reportabilityPredicate,
                dataAcquisitionRequestedTemplate,
                measureEvalMetrics,
                patientStatusBundler,
                measureReportGeneratedProducer,
                blobStorageService,
                evaluateMeasureService,
                recoverer,
                fhirContext);
    }

    @AfterEach
    void tearDown() throws Exception {
        mocks.close();
    }

    private ConsumerRecord<String, EvaluationRequested> buildRecord(String facilityId, EvaluationRequested value) {
        return new ConsumerRecord<>("topic", 0, 0, facilityId, value);
    }

    private PatientReportingEvaluationStatus buildPatientStatus(String facilityId, String patientId, String reportTrackingId) {
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId(reportTrackingId);

        PatientReportingEvaluationStatus status = new PatientReportingEvaluationStatus();
        status.setFacilityId(facilityId);
        status.setPatientId(patientId);
        status.setCorrelationId("correlation-1");
        status.setReports(Collections.singletonList(report));
        return status;
    }

    @Test
    void process_patientStatusNotFound_throwsIllegalStateException() {
        EvaluationRequested value = new EvaluationRequested();
        value.setPatientId("patient-1");
        value.setPreviousReportId("report-1");
        value.setReportTrackingId("tracking-1");

        when(patientStatusRepository.findByFacilityIdAndPatientIdAndReportsReportTrackingId(
                "facility-1", "patient-1", "report-1"))
                .thenReturn(Optional.empty());

        assertThrows(IllegalStateException.class,
                () -> consumer.process(buildRecord("facility-1", value)));

        verifyNoInteractions(evaluateMeasureService);
    }

    @Test
    void process_patientStatusFound_emptyBundle_doesNotCallEvaluateMeasure() {
        EvaluationRequested value = new EvaluationRequested();
        value.setPatientId("patient-1");
        value.setPreviousReportId("report-1");
        value.setReportTrackingId("tracking-1");

        PatientReportingEvaluationStatus patientStatus = buildPatientStatus("facility-1", "patient-1", "report-1");

        when(patientStatusRepository.findByFacilityIdAndPatientIdAndReportsReportTrackingId(
                "facility-1", "patient-1", "report-1"))
                .thenReturn(Optional.of(patientStatus));
        when(patientStatusBundler.createBundle("facility-1", "correlation-1"))
                .thenReturn(new Bundle());

        assertDoesNotThrow(() -> consumer.process(buildRecord("facility-1", value)));

        verifyNoInteractions(evaluateMeasureService);
        verify(measureReportGeneratedProducer).produceMeasureReportGeneratedRecord(any(), any(), any(), isNull(), isNull());
        verifyNoInteractions(blobStorageService);
    }

    @Test
    void process_patientStatusFound_nonEmptyBundle_callsEvaluateMeasure() {
        EvaluationRequested value = new EvaluationRequested();
        value.setPatientId("patient-1");
        value.setPreviousReportId("report-1");
        value.setReportTrackingId("tracking-1");

        PatientReportingEvaluationStatus patientStatus = buildPatientStatus("facility-1", "patient-1", "report-1");

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(new Patient().setId("patient-1"));

        when(patientStatusRepository.findByFacilityIdAndPatientIdAndReportsReportTrackingId(
                "facility-1", "patient-1", "report-1"))
                .thenReturn(Optional.of(patientStatus));
        when(patientStatusBundler.createBundle("facility-1", "correlation-1"))
                .thenReturn(bundle);

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("measure-report-1");
        when(evaluateMeasureService.evaluateMeasure(any(), any(), any())).thenReturn(measureReport);
        when(reportabilityPredicate.test(measureReport)).thenReturn(false);

        assertDoesNotThrow(() -> consumer.process(buildRecord("facility-1", value)));

        ArgumentCaptor<PatientReportingEvaluationStatus.Report> reportCaptor =
                ArgumentCaptor.forClass(PatientReportingEvaluationStatus.Report.class);
        verify(evaluateMeasureService).evaluateMeasure(any(), reportCaptor.capture(), eq(bundle));
        assertEquals("tracking-1", reportCaptor.getValue().getReportTrackingId());
    }

    @Test
    void process_patientStatusFound_nonEmptyBundle_reportable_callsBlobStorage() {
        EvaluationRequested value = new EvaluationRequested();
        value.setPatientId("patient-1");
        value.setPreviousReportId("report-1");
        value.setReportTrackingId("tracking-1");

        PatientReportingEvaluationStatus patientStatus = buildPatientStatus("facility-1", "patient-1", "report-1");

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(new Patient().setId("patient-1"));

        when(patientStatusRepository.findByFacilityIdAndPatientIdAndReportsReportTrackingId(
                "facility-1", "patient-1", "report-1"))
                .thenReturn(Optional.of(patientStatus));
        when(patientStatusBundler.createBundle("facility-1", "correlation-1"))
                .thenReturn(bundle);

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("measure-report-1");
        when(evaluateMeasureService.evaluateMeasure(any(), any(), any())).thenReturn(measureReport);
        when(reportabilityPredicate.test(measureReport)).thenReturn(true);

        assertDoesNotThrow(() -> consumer.process(buildRecord("facility-1", value)));

        verify(blobStorageService).storePatientInBlobStorage(any(), any(), eq(measureReport));
        verifyNoInteractions(measureReportGeneratedProducer);
    }
}
