package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.QueryType;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourcesNormalized;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.listener.ConsumerRecordRecoverer;
import org.springframework.test.util.ReflectionTestUtils;

import java.util.Collections;
import java.util.function.Predicate;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

class ResourcesNormalizedConsumerTest {

    @Mock
    private PatientReportingEvaluationStatusRepository patientStatusRepository;

    @Mock
    private Predicate<MeasureReport> reportabilityPredicate;

    @Mock
    private MeasureEvalMetrics measureEvalMetrics;

    @Mock
    private KafkaTemplate<String, DataAcquisitionRequested> dataAcquisitionRequestedTemplate;

    @Mock
    private EvaluateMeasureService evaluateMeasureService;

    @Mock
    private PatientStatusBundler patientStatusBundler;

    @Mock
    private BlobStorageService blobStorageService;

    @Mock
    private ConsumerRecordRecoverer recoverer;

    @Mock
    private MeasureReportGeneratedProducer measureReportGeneratedProducer;

    @Mock
    private RedisResourceService redisResourceService;

    @Mock
    private AbsResourceService absResourceService;

    @Mock
    private MongoOperations mongoOperations;

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
                absResourceService,
                mongoOperations);
    }

    @AfterEach
    void tearDown() throws Exception {
        mocks.close();
    }

    @Test
    void evaluateMeasures_initialQueryEmptyBundle_doesNotEvaluateAndProducesRecord() {
        // Arrange
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

        when(patientStatusRepository.save(any())).thenReturn(patientStatus);

        // Act & Assert - does not throw
        assertDoesNotThrow(() ->
                ReflectionTestUtils.invokeMethod(consumer, "evaluateMeasures", value, patientStatus, bundle, System.currentTimeMillis())
        );

        // Assert - evaluateMeasureService.evaluateMeasure was not called
        verifyNoInteractions(evaluateMeasureService);

        // Assert - report is not reportable
        assertNotNull(report.getReportable());
        assertFalse(report.getReportable());

        // Assert - measureReportGeneratedProducer.produceMeasureReportGeneratedRecord was called
        verify(measureReportGeneratedProducer).produceMeasureReportGeneratedRecord(
                eq(patientStatus), eq(report), anyString(), isNull(), isNull(), any());
    }
}
