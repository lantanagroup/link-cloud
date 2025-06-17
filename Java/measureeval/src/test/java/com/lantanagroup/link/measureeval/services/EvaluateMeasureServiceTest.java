package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.repositories.PatientReportingEvaluationStatusRepository;
import org.hl7.fhir.r4.model.Bundle;

import org.hl7.fhir.r4.model.MeasureReport;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;


import java.util.Date;

import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.mockito.Mockito.*;

public class EvaluateMeasureServiceTest {

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

    @BeforeEach
    void setUp() {
        MockitoAnnotations.openMocks(this);
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
}