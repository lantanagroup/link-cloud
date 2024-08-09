package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.r4.model.*;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.util.Calendar;

class MeasureEvaluatorEvaluationTests {

    private final FhirContext fhirContext = FhirContext.forR4Cached();

    @Test
    void simpleCohortMeasureTest() {
        MeasureEvaluator evaluator = MeasureEvaluator.compile(fhirContext, KnowledgeArtifactBuilder.CohortMeasure.bundle());
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.patientOnlyBundle());

        // test measurement period results
        var calendar = Calendar.getInstance();
        calendar.setTime(report.getPeriod().getStart());
        Assertions.assertEquals(2024, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(0, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(1, calendar.get(Calendar.DAY_OF_MONTH));
        calendar.setTime(report.getPeriod().getEnd());
        Assertions.assertEquals(2024, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(11, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(31, calendar.get(Calendar.DAY_OF_MONTH));

        // test population results
        Assertions.assertEquals(1, getPopulation("initial-population", report).getCount());
    }

    @Test
    void simpleProportionMeasureTest() {
        MeasureEvaluator evaluator = MeasureEvaluator.compile(fhirContext, KnowledgeArtifactBuilder.ProportionMeasure.bundle());
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.patientOnlyBundle());

        // test measurement period results
        var calendar = Calendar.getInstance();
        calendar.setTime(report.getPeriod().getStart());
        Assertions.assertEquals(2024, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(0, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(1, calendar.get(Calendar.DAY_OF_MONTH));
        calendar.setTime(report.getPeriod().getEnd());
        Assertions.assertEquals(2024, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(11, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(31, calendar.get(Calendar.DAY_OF_MONTH));

        // test measure score
        Assertions.assertEquals(1.0, report.getGroupFirstRep().getMeasureScore().getValue().doubleValue());

        // test population results
        Assertions.assertEquals(1, getPopulation("initial-population", report).getCount());
        Assertions.assertEquals(1, getPopulation("numerator", report).getCount());
        Assertions.assertEquals(0, getPopulation("numerator-exclusion", report).getCount());
        Assertions.assertEquals(1, getPopulation("denominator", report).getCount());
        Assertions.assertEquals(0, getPopulation("denominator-exclusion", report).getCount());
    }

    @Test
    void simpleRatioMeasureTest() {
        MeasureEvaluator evaluator = MeasureEvaluator.compile(fhirContext, KnowledgeArtifactBuilder.RatioMeasure.bundle());
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.patientAndEncounterBundle());

        // test measurement period results
        var calendar = Calendar.getInstance();
        calendar.setTime(report.getPeriod().getStart());
        Assertions.assertEquals(2024, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(0, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(1, calendar.get(Calendar.DAY_OF_MONTH));
        calendar.setTime(report.getPeriod().getEnd());
        Assertions.assertEquals(2024, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(11, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(31, calendar.get(Calendar.DAY_OF_MONTH));

        // test measure score
        Assertions.assertEquals(1.0, report.getGroupFirstRep().getMeasureScore().getValue().doubleValue());

        // test population results
        Assertions.assertEquals(1, getPopulation("initial-population", report).getCount());
        Assertions.assertEquals(1, getPopulation("numerator", report).getCount());
        Assertions.assertEquals(1, getPopulation("denominator", report).getCount());

        // test evaluated resources
        Assertions.assertTrue(report.hasEvaluatedResource());
        Assertions.assertEquals("Encounter/simple-encounter", report.getEvaluatedResourceFirstRep().getReference());
    }

    @Test
    void simpleContinuousVariableMeasureTest() {
        MeasureEvaluator evaluator = MeasureEvaluator.compile(fhirContext, KnowledgeArtifactBuilder.ContinuousVariableMeasure.bundle());
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.patientAndEncounterBundle());

        // test measurement period results
        var calendar = Calendar.getInstance();
        calendar.setTime(report.getPeriod().getStart());
        Assertions.assertEquals(2024, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(0, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(1, calendar.get(Calendar.DAY_OF_MONTH));
        calendar.setTime(report.getPeriod().getEnd());
        Assertions.assertEquals(2024, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(11, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(31, calendar.get(Calendar.DAY_OF_MONTH));

        // test measure score is null
        Assertions.assertNull(report.getGroupFirstRep().getMeasureScore().getValue());

        // test population results
        Assertions.assertEquals(1, getPopulation("initial-population", report).getCount());
        Assertions.assertEquals(1, getPopulation("measure-population", report).getCount());
        Assertions.assertEquals(0, getPopulation("measure-population-exclusion", report).getCount());

        // test evaluated resources
        Assertions.assertTrue(report.hasEvaluatedResource());
        Assertions.assertEquals("Encounter/simple-encounter", report.getEvaluatedResourceFirstRep().getReference());
    }

    private MeasureReport.MeasureReportGroupPopulationComponent getPopulation(String code, MeasureReport report) {
        var population = report.getGroupFirstRep().getPopulation().stream().filter(pop -> pop.getCode().getCodingFirstRep().getCode().equals(code)).toList();
        Assertions.assertEquals(1, population.size());
        return population.get(0);
    }
}
