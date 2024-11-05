package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.r4.model.*;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.Calendar;

class MeasureEvaluatorEvaluationTests {

    private final FhirContext fhirContext = FhirContext.forR4Cached();

    @Test
    void simpleCohortMeasureTrueTest() {
        var measurePackage = KnowledgeArtifactBuilder.SimpleCohortMeasureTrue.bundle();
        validateMeasurePackage(measurePackage);
        var evaluator = MeasureEvaluator.compile(fhirContext, measurePackage);
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.simplePatientOnlyBundle());

        // test measurement period results
        validateMeasurementPeriod(report.getPeriod(), 2024, 0, 1, 2024, 11, 31);

        // test population results
        Assertions.assertEquals(1, getPopulation("initial-population", report).getCount());
    }

    @Test
    void simpleCohortMeasureFalseTest() {
        var measurePackage = KnowledgeArtifactBuilder.SimpleCohortMeasureFalse.bundle();
        validateMeasurePackage(measurePackage);
        var evaluator = MeasureEvaluator.compile(fhirContext, measurePackage);
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.simplePatientOnlyBundle());

        // test measurement period results
        validateMeasurementPeriod(report.getPeriod(), 2024, 0, 1, 2024, 11, 31);

        // test population results
        Assertions.assertEquals(0, getPopulation("initial-population", report).getCount());
    }

    @Test
    void cohortMeasureWithValueSetTrueTest() {
        var measurePackage = KnowledgeArtifactBuilder.CohortMeasureWithValueSetTrue.bundle();
        validateMeasurePackage(measurePackage);
        var evaluator = MeasureEvaluator.compile(fhirContext, measurePackage);
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.simplePatientAndEncounterBundle());

        // test measurement period results
        validateMeasurementPeriod(report.getPeriod(), 2024, 0, 1, 2024, 11, 31);

        // test population results
        Assertions.assertEquals(1, getPopulation("initial-population", report).getCount());

        // test evaluated resources
        Assertions.assertTrue(report.hasEvaluatedResource());
        Assertions.assertEquals(2, report.getEvaluatedResource().size());
        Assertions.assertTrue(report.getEvaluatedResourceFirstRep().hasReference());
        Assertions.assertEquals("Encounter/simple-encounter", report.getEvaluatedResourceFirstRep().getReference());
        Assertions.assertTrue(report.getEvaluatedResource().get(1).hasReference());
        Assertions.assertEquals("Patient/simple-patient", report.getEvaluatedResource().get(1).getReference());
    }

    @Test
    void cohortMeasureWithValueSetFalseTest() {
        var measurePackage = KnowledgeArtifactBuilder.CohortMeasureWithValueSetFalse.bundle();
        validateMeasurePackage(measurePackage);
        var evaluator = MeasureEvaluator.compile(fhirContext, measurePackage);
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.simplePatientAndEncounterBundle());

        // test measurement period results
        validateMeasurementPeriod(report.getPeriod(), 2024, 0, 1, 2024, 11, 31);

        // test population results
        Assertions.assertEquals(0, getPopulation("initial-population", report).getCount());
    }

    @Test
    void cohortMeasureWithSDETest() {
        var measurePackage = KnowledgeArtifactBuilder.CohortMeasureWithSDE.bundle();
        validateMeasurePackage(measurePackage);
        var evaluator = MeasureEvaluator.compile(fhirContext, measurePackage);
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.simplePatientEncounterAndConditionBundle());

        // test measurement period results
        validateMeasurementPeriod(report.getPeriod(), 2024, 0, 1, 2024, 11, 31);

        // test population results
        Assertions.assertEquals(1, getPopulation("initial-population", report).getCount());

        // test evaluated resources
        Assertions.assertTrue(report.hasEvaluatedResource());
        Assertions.assertEquals(3, report.getEvaluatedResource().size());

        // test extensions, references, and contained
        Assertions.assertTrue(report.hasExtension("http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalDataElement.reference"));
        var extension = report.getExtensionByUrl("http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalDataElement.reference");
        Assertions.assertTrue(extension.hasValue());
        Assertions.assertTrue(extension.getValue() instanceof Reference);
        var reference = (Reference) extension.getValue();
        Assertions.assertEquals("#TST-simple-condition", reference.getReference());
        Assertions.assertTrue(report.hasContained());
        Assertions.assertEquals(1, report.getContained().size());
        Assertions.assertTrue(report.getContained().get(0) instanceof Condition);
        var condition = (Condition) report.getContained().get(0);
        Assertions.assertEquals("TST-simple-condition", condition.getIdPart());
    }

    @Test
    void simpleProportionMeasureAllTrueNoExclusionTest() {
        var measurePackage = KnowledgeArtifactBuilder.SimpleProportionMeasureAllTrueNoExclusion.bundle();
        validateMeasurePackage(measurePackage);
        var evaluator = MeasureEvaluator.compile(fhirContext, measurePackage);
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.simplePatientOnlyBundle());

        // test measurement period results
        validateMeasurementPeriod(report.getPeriod(), 2024, 0, 1, 2024, 11, 31);

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
    void simpleProportionMeasureAllFalseTest() {
        var measurePackage = KnowledgeArtifactBuilder.SimpleProportionMeasureAllFalse.bundle();
        validateMeasurePackage(measurePackage);
        var evaluator = MeasureEvaluator.compile(fhirContext, measurePackage);
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.simplePatientOnlyBundle());

        // test measurement period results
        validateMeasurementPeriod(report.getPeriod(), 2024, 0, 1, 2024, 11, 31);

        // test measure score
        Assertions.assertNull(report.getGroupFirstRep().getMeasureScore().getValue());

        // test population results
        Assertions.assertEquals(0, getPopulation("initial-population", report).getCount());
        Assertions.assertEquals(0, getPopulation("numerator", report).getCount());
        Assertions.assertEquals(0, getPopulation("numerator-exclusion", report).getCount());
        Assertions.assertEquals(0, getPopulation("denominator", report).getCount());
        Assertions.assertEquals(0, getPopulation("denominator-exclusion", report).getCount());
    }

    @Test
    void simpleRatioMeasureTest() {
        var measurePackage = KnowledgeArtifactBuilder.SimpleRatioMeasure.bundle();
        validateMeasurePackage(measurePackage);
        var evaluator = MeasureEvaluator.compile(fhirContext, measurePackage);
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.simplePatientAndEncounterBundle());

        // test measurement period results
        validateMeasurementPeriod(report.getPeriod(), 2024, 0, 1, 2024, 11, 31);

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
        var measurePackage = KnowledgeArtifactBuilder.SimpleContinuousVariableMeasure.bundle();
        validateMeasurePackage(measurePackage);
        var evaluator = MeasureEvaluator.compile(fhirContext, KnowledgeArtifactBuilder.SimpleContinuousVariableMeasure.bundle());
        var report = evaluator.evaluate(new DateTimeType("2024-01-01"), new DateTimeType("2024-12-31"),
                new StringType("Patient/simple-patient"), PatientDataBuilder.simplePatientAndEncounterBundle());

        // test measurement period results
        validateMeasurementPeriod(report.getPeriod(), 2024, 0, 1, 2024, 11, 31);

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

    private void validateMeasurePackage(Bundle measurePackage) {
        var errors = new ArrayList<String>();
        var bundleValidator = new MeasureDefinitionBundleValidator();
        bundleValidator.doValidate(measurePackage, errors);
        Assertions.assertTrue(errors.isEmpty());
    }

    private void validateMeasurementPeriod(
            Period period, int expectedStartYear, int expectedStartMonth, int expectedStartDay,
            int expectedEndYear, int expectedEndMonth, int expectedEndDay) {
        var calendar = Calendar.getInstance();
        calendar.setTime(period.getStart());
        Assertions.assertEquals(expectedStartYear, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(expectedStartMonth, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(expectedStartDay, calendar.get(Calendar.DAY_OF_MONTH));
        calendar.setTime(period.getEnd());
        Assertions.assertEquals(expectedEndYear, calendar.get(Calendar.YEAR));
        Assertions.assertEquals(expectedEndMonth, calendar.get(Calendar.MONTH)); // 0-based months
        Assertions.assertEquals(expectedEndDay, calendar.get(Calendar.DAY_OF_MONTH));
    }

    private MeasureReport.MeasureReportGroupPopulationComponent getPopulation(String code, MeasureReport report) {
        var population = report.getGroupFirstRep().getPopulation().stream().filter(pop -> pop.getCode().getCodingFirstRep().getCode().equals(code)).toList();
        Assertions.assertEquals(1, population.size());
        return population.get(0);
    }
}
