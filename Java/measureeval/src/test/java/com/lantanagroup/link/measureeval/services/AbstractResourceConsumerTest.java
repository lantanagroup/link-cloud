package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.*;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourcesNormalized;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import org.apache.kafka.clients.producer.ProducerRecord;

import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.ResourceType;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.test.util.ReflectionTestUtils;

import java.util.*;
import java.util.function.Predicate;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

class AbstractResourceConsumerTest {

    @Mock private PatientReportingEvaluationStatusRepository patientStatusRepository;
    @Mock private Predicate<MeasureReport> reportabilityPredicate;
    @Mock private MeasureEvalMetrics measureEvalMetrics;
    @Mock private KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;
    @Mock private EvaluateMeasureService evaluateMeasureService;
    @Mock private PatientStatusBundler patientStatusBundler;
    @Mock private BlobStorageService blobStorageService;
    @Mock private ConsumerRecordRecoverer recoverer;
    @Mock private MeasureReportGeneratedProducer measureReportGeneratedProducer;
    @Mock private RedisResourceService redisResourceService;
    @Mock private MongoOperations mongoOperations;

    private AutoCloseable mocks;
    private ResourcesNormalizedConsumer consumer;

    @BeforeEach
    void setUp() {
        mocks = MockitoAnnotations.openMocks(this);
        consumer = new ResourcesNormalizedConsumer(
                patientStatusRepository,
                reportabilityPredicate,
                measureEvalMetrics,
                dataAcquisitionRequestedTemplate,
                evaluateMeasureService,
                patientStatusBundler,
                blobStorageService,
                recoverer,
                measureReportGeneratedProducer,
                redisResourceService,
                mongoOperations);
    }

    @AfterEach
    void tearDown() throws Exception {
        mocks.close();
    }

    @Test
    void buildDeterministicId_returnsConsistentHash() {
        Resource r = new Resource();
        r.setFacilityId("MyFacility");
        r.setCorrelationId("corr-123");
        r.setResourceType(ResourceType.Patient);
        r.setResourceId("p-456");

        String id1 = ReflectionTestUtils.invokeMethod(consumer, "buildDeterministicId", r);
        String id2 = ReflectionTestUtils.invokeMethod(consumer, "buildDeterministicId", r);

        assertNotNull(id1);
        assertEquals(36, id1.length());
        assertEquals(id1, id2);
    }

    @Test
    void evaluateMeasures_supplementalReportable_storesInBlobStorage() {
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.SUPPLEMENTAL);

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportable(true);
        report.setReportTrackingId("tracking-1");
        report.setReportType("TestMeasure");

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");
        patientStatus.setPatientId("patient-1");
        patientStatus.setReports(Collections.singletonList(report));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(new org.hl7.fhir.r4.model.Patient());

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        verify(blobStorageService).storePatientInBlobStorage(eq(patientStatus), eq(report), eq(measureReport));
    }

    @Test
    void evaluateMeasures_supplementalNotReportable_skipsEvaluation() {
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.SUPPLEMENTAL);

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportable(false);
        report.setReportTrackingId("tracking-1");

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");
        patientStatus.setPatientId("patient-1");
        patientStatus.setReports(Collections.singletonList(report));

        verifyNoInteractions(evaluateMeasureService);
        verifyNoInteractions(blobStorageService);
    }

    @Test
    void evaluateMeasures_initialReportable_producesDataAcquisitionRequested() {
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.INITIAL);
        value.setReportableEvent(ReportableEvent.valueOf("Adhoc"));

        TestScheduledReport sr = new TestScheduledReport();
        sr.reportTypes = new String[]{"TestMeasure"};
        sr.frequency = "Adhoc";
        sr.startDate = new Date();
        sr.endDate = new Date();
        sr.reportTrackingId = "tracking-1";
        value.setScheduledReports(List.of(sr.toScheduledReport()));

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportable(null);
        report.setReportTrackingId("tracking-1");
        report.setReportType("TestMeasure");

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");
        patientStatus.setPatientId("patient-1");
        patientStatus.setReports(Collections.singletonList(report));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(new org.hl7.fhir.r4.model.Patient());

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        when(reportabilityPredicate.test(any())).thenReturn(true);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        boolean result = Boolean.TRUE.equals(ReflectionTestUtils.invokeMethod(
                consumer, "evaluateMeasures", value, patientStatus, bundle, System.currentTimeMillis()));

        assertTrue(result);
        assertTrue(report.getReportable());
        verify(dataAcquisitionRequestedTemplate).send((ProducerRecord<String, DataAcquisitionRequested>) any());
    }

    @Test
    void evaluateMeasures_initialNotReportable_producesMeasureReportGenerated() {
        ResourcesNormalized value = new ResourcesNormalized();
        value.setQueryType(QueryType.INITIAL);

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportable(null);
        report.setReportTrackingId("tracking-1");

        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");
        patientStatus.setPatientId("patient-1");
        patientStatus.setReports(Collections.singletonList(report));

        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(new org.hl7.fhir.r4.model.Patient());

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr-1");
        when(evaluateMeasureService.evaluateMeasure(anyString(), any(), any(), any())).thenReturn(measureReport);
        when(reportabilityPredicate.test(any())).thenReturn(false);
        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        boolean result = Boolean.TRUE.equals(ReflectionTestUtils.invokeMethod(
                consumer, "evaluateMeasures", value, patientStatus, bundle, System.currentTimeMillis()));

        assertFalse(result);
        assertFalse(report.getReportable());
        verify(measureReportGeneratedProducer).produceMeasureReportGeneratedRecord(
                eq(patientStatus), eq(report), anyString(), isNull(), isNull());
    }

    private static class TestScheduledReport {
        String[] reportTypes;
        String frequency;
        Date startDate;
        Date endDate;
        String reportTrackingId;

        com.lantanagroup.link.measureeval.records.AbstractResourceRecord.ScheduledReport toScheduledReport() {
            var sr = new com.lantanagroup.link.measureeval.records.AbstractResourceRecord.ScheduledReport();
            sr.setReportTypes(reportTypes);
            sr.setFrequency(frequency);
            sr.setStartDate(startDate);
            sr.setEndDate(endDate);
            sr.setReportTrackingId(reportTrackingId);
            return sr;
        }
    }
}
