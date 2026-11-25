package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.models.DebugSections;
import com.lantanagroup.link.measureeval.models.MeasureEvaluationResult;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.DateTimeType;
import org.hl7.fhir.r4.model.Enumeration;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.MedicationRequest;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.StringType;
import org.hl7.fhir.r4.model.Timing;
import org.hl7.fhir.r4.model.ValueSet;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.EnumSet;
import java.util.List;
import java.util.stream.Collectors;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;

/**
 * Evaluates the NHSN Acute Care Hospital Daily measure bundle from {@code DotNet/Automation/measures}
 * against a small synthetic patient, so behaviour that depends on the real NHSN CQL (its Instance
 * selectors that rebuild trimmed resources) is covered, not just the toy measures in
 * {@link KnowledgeArtifactBuilder}.
 */
class MeasureEvaluatorNhsnBundleTests {

    private static final String DAILY = "NHSNAcuteCareHospitalDailyInitialPopulation";
    private static final String PERIOD_START = "2022-01-01T00:00:00Z";
    private static final String PERIOD_END = "2022-01-01T23:59:59Z";
    private static final String SUBJECT = "Patient/p1";

    private static final FhirContext fhirContext = FhirContext.forR4Cached();
    private static Bundle dailyBundle;

    @BeforeAll
    static void loadDailyBundle() throws Exception {
        Path repoRoot = Path.of("").toAbsolutePath();
        while (repoRoot != null && !Files.exists(measurePath(repoRoot))) {
            repoRoot = repoRoot.getParent();
        }
        Assumptions.assumeTrue(repoRoot != null,
                "Skipping: could not locate DotNet/Automation/measures from " + Path.of("").toAbsolutePath());
        String json = Files.readString(measurePath(repoRoot), StandardCharsets.UTF_8);
        if (json.startsWith("\uFEFF")) {
            json = json.substring(1);
        }
        dailyBundle = fhirContext.newJsonParser().parseResource(Bundle.class, json);
    }

    private static Path measurePath(Path repoRoot) {
        return repoRoot.resolve("DotNet/Automation/measures/" + DAILY + ".json");
    }

    /**
     * An inpatient encounter plus a Remdesivir order whose dosage timing repeats on given days and at
     * given times of day. {@code repeat.when} and {@code repeat.dayOfWeek} are both 0..* code elements.
     */
    private static Bundle patientData(String repeatJson) {
        String json = """
                {"resourceType":"Bundle","type":"collection","entry":[
                 {"resource":{"resourceType":"Patient","id":"p1","gender":"female","birthDate":"1970-01-01"}},
                 {"resource":{"resourceType":"Encounter","id":"e1","status":"in-progress",
                   "class":{"system":"http://terminology.hl7.org/CodeSystem/v3-ActCode","code":"IMP"},
                   "type":[{"coding":[{"system":"http://snomed.info/sct","code":"183452005"}]}],
                   "subject":{"reference":"Patient/p1"},"period":{"start":"2022-01-01T00:00:00Z"}}},
                 {"resource":{"resourceType":"MedicationRequest","id":"mr1","status":"active","intent":"order",
                   "medicationCodeableConcept":{"coding":[{"system":"http://www.nlm.nih.gov/research/umls/rxnorm","code":"2284718"}]},
                   "subject":{"reference":"Patient/p1"},"encounter":{"reference":"Encounter/e1"},
                   "authoredOn":"2022-01-01T08:00:00Z",
                   "dosageInstruction":[{"text":"x","timing":{"repeat":REPEAT}}]}}]}
                """.replace("REPEAT", repeatJson);
        return fhirContext.newJsonParser().parseResource(Bundle.class, json);
    }

    private static final String REPEAT_WITH_LISTS =
            "{\"frequency\":1,\"period\":1,\"periodUnit\":\"d\",\"dayOfWeek\":[\"mon\",\"wed\"],\"when\":[\"MORN\"]}";

    private static int initialPopulation(MeasureReport report) {
        return report.getGroupFirstRep().getPopulation().stream()
                .filter(p -> "initial-population".equals(p.getCode().getCodingFirstRep().getCode()))
                .findFirst()
                .orElseThrow()
                .getCount();
    }

    private static List<String> issues(MeasureReport report) {
        return report.getContained().stream()
                .filter(OperationOutcome.class::isInstance)
                .map(OperationOutcome.class::cast)
                .flatMap(oo -> oo.getIssue().stream())
                .map(OperationOutcome.OperationOutcomeIssueComponent::getDiagnostics)
                .toList();
    }

    private static String codes(List<? extends Enumeration<?>> values) {
        return values.stream().map(Enumeration::getValueAsString).collect(Collectors.joining(","));
    }

    /**
     * Regression: cqf-fhir 4.5.1 threw a NullPointerException in DynamicModelResolver.setValue when the
     * NHSN CQL rebuilt a Timing whose repeat held list-valued codes, and the patient dropped out of
     * the Initial Population.
     */
    @Test
    void listValuedTimingCodes_keepPatientInInitialPopulationAndSurviveTrimming() {
        MeasureEvaluator evaluator = MeasureEvaluator.compile(fhirContext, dailyBundle, false);

        MeasureReport report = evaluator.evaluate(
                new DateTimeType(PERIOD_START), new DateTimeType(PERIOD_END), new StringType(SUBJECT),
                patientData(REPEAT_WITH_LISTS));

        assertEquals(List.of(), issues(report), "evaluation should record no errors");
        assertEquals(1, initialPopulation(report));

        MedicationRequest medicationRequest = report.getContained().stream()
                .filter(MedicationRequest.class::isInstance)
                .map(MedicationRequest.class::cast)
                .findFirst()
                .orElseThrow(() -> new AssertionError("the MedicationRequest should be in the report"));
        Timing.TimingRepeatComponent repeat =
                medicationRequest.getDosageInstructionFirstRep().getTiming().getRepeat();
        assertEquals("mon,wed", codes(repeat.getDayOfWeek()));
        assertEquals("MORN", codes(repeat.getWhen()));
    }

    /**
     * CQL engine 5 hands back its own runtime values rather than HAPI types; the debug output must
     * still render resources as references, not as full structure dumps.
     */
    @Test
    void expressionResults_renderEngineValuesCompactly() {
        Parameters parameters = new Parameters();
        parameters.addParameter().setName("periodStart").setValue(new DateTimeType(PERIOD_START));
        parameters.addParameter().setName("periodEnd").setValue(new DateTimeType(PERIOD_END));
        parameters.addParameter().setName("subject").setValue(new StringType(SUBJECT));
        parameters.addParameter().setName("additionalData").setResource(patientData(REPEAT_WITH_LISTS));

        MeasureEvaluationResult result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, dailyBundle, parameters, EnumSet.of(DebugSections.EXPRESSIONS));

        var expressionResults = result.getDebugInfo().getExpressionResults();
        assertNotNull(expressionResults);
        assertFalse(expressionResults.isEmpty());
        expressionResults.forEach((name, value) ->
                assertFalse(value.contains("FHIR."), name + " was rendered as a structure dump: " + value));
        assertEquals("[Encounter/e1]", expressionResults.get("EncounterInpatient"));
        assertEquals("Patient/p1", expressionResults.get("Patient"));
        // Resources the NHSN CQL rebuilds with Instance selectors keep their LCR- ids.
        assertEquals("[MedicationRequest/LCR-mr1]", expressionResults.get("SDE Covid or Influenza Medication Ordered"));
    }

    /**
     * Measure artifacts and patient data share one repository; a patient resource with the same type
     * and id as an artifact must not break evaluation or replace the artifact.
     */
    @Test
    void patientResourceCollidingWithMeasureArtifact_isIgnored() {
        MeasureEvaluator evaluator = MeasureEvaluator.compile(fhirContext, dailyBundle, false);
        ValueSet artifact = dailyBundle.getEntry().stream()
                .map(Bundle.BundleEntryComponent::getResource)
                .filter(ValueSet.class::isInstance)
                .map(ValueSet.class::cast)
                .findFirst()
                .orElseThrow();
        Bundle data = patientData(REPEAT_WITH_LISTS);
        ValueSet impostor = new ValueSet();
        impostor.setId(artifact.getIdElement().getIdPart());
        impostor.setUrl(artifact.getUrl());
        data.addEntry().setResource(impostor);

        MeasureReport report = evaluator.evaluate(
                new DateTimeType(PERIOD_START), new DateTimeType(PERIOD_END), new StringType(SUBJECT), data);

        assertEquals(List.of(), issues(report));
        assertEquals(1, initialPopulation(report));
    }
}
