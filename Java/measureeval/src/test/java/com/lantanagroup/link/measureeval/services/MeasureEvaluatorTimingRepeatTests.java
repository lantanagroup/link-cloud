package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.models.DebugSections;
import org.hl7.fhir.r4.model.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;

import java.util.EnumSet;
import java.util.List;
import java.util.stream.Stream;

import static org.junit.jupiter.api.Assertions.*;

/**
 * Evaluates a measure whose SDE rebuilds MedicationRequest field by field down to Timing.Repeat, the shape the
 * NHSN ACH 3.0.0 measures use. Before clinical-reasoning 4.9.0, a repeat carrying the list-valued enumeration
 * elements {@code dayOfWeek} or {@code when} failed the whole subject with a NullPointerException.
 */
class MeasureEvaluatorTimingRepeatTests {

    private static final String SUBJECT = "Patient/simple-patient";

    private final FhirContext fhirContext = FhirContext.forR4Cached();

    static Stream<Arguments> repeats() {
        return Stream.of(
                Arguments.of("single dayOfWeek",
                             new Timing.TimingRepeatComponent()
                                     .addDayOfWeek(Timing.DayOfWeek.MON)),
                Arguments.of("single when",
                             new Timing.TimingRepeatComponent()
                                     .addWhen(Timing.EventTiming.MORN)),
                Arguments.of("several dayOfWeek and when values",
                             new Timing.TimingRepeatComponent()
                                     .setFrequency(2)
                                     .addDayOfWeek(Timing.DayOfWeek.MON)
                                     .addDayOfWeek(Timing.DayOfWeek.WED)
                                     .addWhen(Timing.EventTiming.MORN)
                                     .addWhen(Timing.EventTiming.EVE)),
                Arguments.of("no list-valued elements",
                             new Timing.TimingRepeatComponent()
                                     .setFrequency(1)));
    }

    /**
     * The subject evaluates, and the rebuilt MedicationRequest keeps every repeat element it was given.
     */
    @ParameterizedTest(name = "{0}")
    @MethodSource("repeats")
    void rebuiltRepeatEvaluatesAndKeepsValuesTest(String name, Timing.TimingRepeatComponent repeat) {
        var evaluator = MeasureEvaluator.compile(fhirContext, measureBundle(), false);

        var report = evaluator.evaluate(new DateTimeType("2024-01-01"),
                                        new DateTimeType("2024-12-31"),
                                        new StringType(SUBJECT),
                                        patientData(repeat.copy()));

        assertEquals(MeasureReport.MeasureReportStatus.COMPLETE, report.getStatus());
        assertEquals(1, report.getGroupFirstRep().getPopulationFirstRep().getCount());
        assertRebuiltRepeatEquals(repeat, report);
    }

    /**
     * The debug path goes through a different clinical-reasoning entry point, so it is covered separately.
     */
    @Test
    void rebuiltRepeatEvaluatesOnDebugPathTest() {
        var repeat = new Timing.TimingRepeatComponent()
                .addDayOfWeek(Timing.DayOfWeek.MON)
                .addWhen(Timing.EventTiming.MORN);
        var parameters = new Parameters();
        parameters.addParameter().setName("periodStart").setValue(new DateTimeType("2024-01-01"));
        parameters.addParameter().setName("periodEnd").setValue(new DateTimeType("2024-12-31"));
        parameters.addParameter().setName("subject").setValue(new StringType(SUBJECT));
        parameters.addParameter().setName("additionalData").setResource(patientData(repeat.copy()));

        var result = MeasureEvaluator.compileAndEvaluate(fhirContext,
                                                         measureBundle(),
                                                         parameters,
                                                         EnumSet.of(DebugSections.GROUPS, DebugSections.EXPRESSIONS));

        var report = result.getMeasureReport();
        assertEquals(MeasureReport.MeasureReportStatus.COMPLETE, report.getStatus());
        assertRebuiltRepeatEquals(repeat, report);
        var debugInfo = result.getDebugInfo();
        assertTrue(debugInfo.getErrors().isEmpty(), () -> "unexpected errors: " + debugInfo.getErrors());
        assertTrue(debugInfo.getExpressionResults().containsKey("SDE Medication Request"));
    }

    private static Bundle measureBundle() {
        return KnowledgeArtifactBuilder.CohortMeasureWithRebuiltMedicationRequestSDE.bundle();
    }

    private static Bundle patientData(Timing.TimingRepeatComponent repeat) {
        var medicationRequest = new MedicationRequest();
        medicationRequest.setId("MedicationRequest/simple-medication-request");
        medicationRequest.setStatus(MedicationRequest.MedicationRequestStatus.ACTIVE);
        medicationRequest.setIntent(MedicationRequest.MedicationRequestIntent.ORDER);
        medicationRequest.setMedication(new CodeableConcept().setText("example"));
        medicationRequest.setSubject(new Reference(SUBJECT));
        medicationRequest.setAuthoredOnElement(new DateTimeType("2024-01-10"));
        medicationRequest.addDosageInstruction().setTiming(new Timing().setRepeat(repeat));

        var bundle = PatientDataBuilder.simplePatientAndEncounterBundle();
        bundle.addEntry().setResource(medicationRequest);
        return bundle;
    }

    private static void assertRebuiltRepeatEquals(Timing.TimingRepeatComponent expected, MeasureReport report) {
        var rebuilt = report.getContained()
                .stream()
                .filter(MedicationRequest.class::isInstance)
                .map(MedicationRequest.class::cast)
                .toList();
        assertEquals(1, rebuilt.size(), "expected one contained MedicationRequest from the SDE");
        assertEquals("TST-simple-medication-request", rebuilt.get(0).getIdPart());

        var actual = rebuilt.get(0)
                .getDosageInstructionFirstRep()
                .getTiming()
                .getRepeat();
        assertEquals(codes(expected.getDayOfWeek()), codes(actual.getDayOfWeek()));
        assertEquals(codes(expected.getWhen()), codes(actual.getWhen()));
        assertEquals(expected.getFrequency(), actual.getFrequency());
    }

    private static List<String> codes(List<? extends Enumeration<?>> values) {
        return values.stream()
                .map(Enumeration::getValueAsString)
                .toList();
    }
}
