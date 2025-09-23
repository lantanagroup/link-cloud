package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import com.lantanagroup.link.shared.utils.DiagnosticNames;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;

import java.text.SimpleDateFormat;
import java.util.Date;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

class EvaluateMeasureServiceTest {

    @Mock
    private PatientReportingEvaluationStatusRepository patientStatusRepository;

    @Mock
    private MeasureEvaluatorCache measureEvaluatorCache;

    @Mock
    private MeasureEvaluator measureEvaluator;

    @InjectMocks
    private EvaluateMeasureService evaluateMeasureService;

    @Mock
    private MeasureEvalMetrics measureEvalMetrics;


    private SimpleDateFormat sdf = new SimpleDateFormat("yyyy-MM-dd");

    @BeforeEach
    void setup() {
        MockitoAnnotations.openMocks(this);
        measureEvaluatorCache = mock(MeasureEvaluatorCache.class);
        measureEvalMetrics = mock(MeasureEvalMetrics.class);
        evaluateMeasureService = new EvaluateMeasureService(measureEvaluatorCache, measureEvalMetrics);
        measureEvaluator = mock(MeasureEvaluator.class);
    }

    @Test
    void testEvaluateMeasure() {
        // Arrange
        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setPatientId("Patient/simple-patient");

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportType("measureId");
        report.setStartDate(new Date()); // Set startDate to today
        report.setEndDate(new Date()); // Set endDate to today
        Bundle bundle = KnowledgeArtifactBuilder.SimpleCohortMeasureTrue.bundle();

        MeasureReport mockMeasureReport = new MeasureReport();
        MeasureReport.MeasureReportGroupComponent group = new MeasureReport.MeasureReportGroupComponent();
        MeasureReport.MeasureReportGroupPopulationComponent population = new MeasureReport.MeasureReportGroupPopulationComponent();
        population.setCount(100);
        group.addPopulation(population);
        mockMeasureReport.addGroup(group);

        when(patientStatusRepository.save(any(PatientReportingEvaluationStatus.class))).thenReturn(patientStatus);
        when(patientStatusRepository.insert(any(PatientReportingEvaluationStatus.class))).thenReturn(patientStatus);
        when(measureEvaluatorCache.get(anyString())).thenReturn(measureEvaluator);
        when(measureEvaluatorCache.get("measureId")).thenReturn(measureEvaluator);
        when(measureEvaluator.evaluate(any(Date.class), any(Date.class), any(String.class), any(Bundle.class))).thenReturn(mockMeasureReport);

        // Act
        MeasureReport result = evaluateMeasureService.evaluateMeasure(patientStatus, report, bundle);

        // Assert
        assertNotNull(result);
    }

    @Test
    void testEvaluateMeasure_returnsMeasureReport() throws Exception {
        // Arrange
        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setPatientId("patient-1");
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        Date startDate = sdf.parse("2025-01-01");
        Date endDate = sdf.parse("2025-01-31");
        report.setStartDate(startDate);
        report.setEndDate(endDate);
        report.setReportType("measure-1");
        report.setFrequency("monthly");

        Bundle bundle = new Bundle();

        MeasureReport expectedReport = new MeasureReport();

        when(measureEvaluatorCache.get("measure-1")).thenReturn(measureEvaluator);
        when(measureEvaluator.evaluate(any(Date.class), any(Date.class), eq("patient-1"), eq(bundle)))
                .thenReturn(expectedReport);

        // Act
        MeasureReport actualReport = evaluateMeasureService.evaluateMeasure(patientStatus, report, bundle);

        // Assert
        assertNotNull(actualReport);
        assertEquals(expectedReport, actualReport);

        // Verify metric recording
        ArgumentCaptor<Long> durationCaptor = ArgumentCaptor.forClass(Long.class);
        verify(measureEvalMetrics).MeasureEvalDuration(durationCaptor.capture(), any());
        assertTrue(durationCaptor.getValue() >= 0);
    }

    @Test
    void testEvaluateMeasure_unknownMeasure_throwsException() throws Exception {
        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setPatientId("patient-2");

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportType("unknown-measure");

        Bundle bundle = new Bundle();

        when(measureEvaluatorCache.get("unknown-measure")).thenReturn(null);

        IllegalStateException exception = assertThrows(IllegalStateException.class, () ->
                evaluateMeasureService.evaluateMeasure(patientStatus, report, bundle)
        );

        assertEquals("Unknown measure: unknown-measure", exception.getMessage());
    }

    @Test
    void testEvaluateMeasureWithQueryType_returnsMeasureReport() throws Exception {
        PatientReportingEvaluationStatus patientStatus = new PatientReportingEvaluationStatus();
        patientStatus.setPatientId("patient-1");
        patientStatus.setFacilityId("facility-1");
        patientStatus.setCorrelationId("correlation-1");

        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        Date startDate = sdf.parse("2025-01-01");
        Date endDate = sdf.parse("2025-01-31");
        report.setStartDate(startDate);
        report.setEndDate(endDate);
        report.setReportType("measure-1");
        report.setFrequency("monthly");

        Bundle bundle = new Bundle();
        String queryType = "test-query";

        MeasureReport expectedReport = new MeasureReport();

        when(measureEvaluatorCache.get("measure-1")).thenReturn(measureEvaluator);
        when(measureEvaluator.evaluate(any(Date.class), any(Date.class), eq("patient-1"), eq(bundle)))
                .thenReturn(expectedReport);

        MeasureReport actualReport = evaluateMeasureService.evaluateMeasure(queryType, patientStatus, report, bundle);

        assertNotNull(actualReport);
        assertEquals(expectedReport, actualReport);

        // Verify metric recording with QUERY_TYPE attribute
        ArgumentCaptor<Long> durationCaptor = ArgumentCaptor.forClass(Long.class);
        ArgumentCaptor<io.opentelemetry.api.common.Attributes> attributesCaptor =
                ArgumentCaptor.forClass(io.opentelemetry.api.common.Attributes.class);
        verify(measureEvalMetrics).MeasureEvalDuration(durationCaptor.capture(), attributesCaptor.capture());

        io.opentelemetry.api.common.Attributes attributes = attributesCaptor.getValue();
        assertEquals("facility-1", attributes.get(io.opentelemetry.api.common.AttributeKey.stringKey(DiagnosticNames.FACILITY_ID)));
        assertEquals("patient-1", attributes.get(io.opentelemetry.api.common.AttributeKey.stringKey(DiagnosticNames.PATIENT_ID)));
        assertEquals("measure-1", attributes.get(io.opentelemetry.api.common.AttributeKey.stringKey(DiagnosticNames.REPORT_TYPE)));
        assertEquals("test-query", attributes.get(io.opentelemetry.api.common.AttributeKey.stringKey(DiagnosticNames.QUERY_TYPE)));
    }
}
