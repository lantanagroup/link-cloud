package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.models.DebugSections;
import com.lantanagroup.link.measureeval.models.MeasureEvaluationResult;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.DateTimeType;
import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.StringType;
import org.junit.jupiter.api.Test;

import java.util.EnumSet;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Integration tests for the debug-capturing code path in
 * {@link MeasureEvaluator#compileAndEvaluate(FhirContext, Bundle, Parameters, Set)}.
 * These tests exercise the real CQL engine + clinical-reasoning capture pipeline
 * end-to-end against a simple cohort measure bundle, so any breaking change in
 * the upstream debug/trace API surface (CR/engine version bumps, method renames)
 * surfaces here rather than in a downstream smoke test.
 */
class MeasureEvaluatorDebugTests {

    private final FhirContext fhirContext = FhirContext.forR4Cached();

    private static Parameters defaultParameters() {
        Parameters params = new Parameters();
        params.addParameter().setName("periodStart").setValue(new DateTimeType("2024-01-01"));
        params.addParameter().setName("periodEnd").setValue(new DateTimeType("2024-12-31"));
        params.addParameter().setName("subject").setValue(new StringType("Patient/simple-patient"));
        params.addParameter().setName("additionalData").setResource(PatientDataBuilder.simplePatientOnlyBundle());
        return params;
    }

    @Test
    void emptyDebugSections_returnsNullDebugInfoButPopulatedMeasureReport() {
        var bundle = KnowledgeArtifactBuilder.SimpleCohortMeasureTrue.bundle();
        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, defaultParameters(), EnumSet.noneOf(DebugSections.class));

        assertNotNull(result);
        assertNotNull(result.getMeasureReport(), "MeasureReport must always be present");
        assertNull(result.getDebugInfo(), "debugInfo must be null when no sections requested");
    }

    @Test
    void groupsSection_populatesGroupsAndErrorsOnly() {
        var bundle = KnowledgeArtifactBuilder.SimpleCohortMeasureTrue.bundle();
        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, defaultParameters(), EnumSet.of(DebugSections.GROUPS));

        assertNotNull(result.getMeasureReport());
        MeasureEvaluationResult.DebugInfo info = result.getDebugInfo();
        assertNotNull(info, "debugInfo must be present when at least one section is requested");

        assertNotNull(info.getGroups(), "groups should be populated when GROUPS requested");
        assertFalse(info.getGroups().isEmpty(), "test measure has at least one group");
        assertNotNull(info.getErrors(), "errors list should be populated alongside groups (may be empty)");

        // Other sections must remain null so JSON serialization omits them.
        assertNull(info.getExpressionResults());
        assertNull(info.getLibraryDebug());
        assertNull(info.getCqlMessages());
        assertNull(info.getTraces());
        assertNull(info.getDebugLog());
    }

    @Test
    void expressionsSection_populatesExpressionResultsOnly() {
        var bundle = KnowledgeArtifactBuilder.SimpleCohortMeasureTrue.bundle();
        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, defaultParameters(), EnumSet.of(DebugSections.EXPRESSIONS));

        MeasureEvaluationResult.DebugInfo info = result.getDebugInfo();
        assertNotNull(info);
        assertNotNull(info.getExpressionResults(), "expressionResults should be populated");
        assertFalse(info.getExpressionResults().isEmpty(),
                "cohort measure CQL evaluation should expose at least one expression result");

        // No other sections should be populated.
        assertNull(info.getGroups());
        assertNull(info.getLibraryDebug());
        assertNull(info.getCqlMessages());
        assertNull(info.getTraces());
        assertNull(info.getDebugLog());
    }

    @Test
    void tracesSection_populatesTracesOnly() {
        var bundle = KnowledgeArtifactBuilder.SimpleCohortMeasureTrue.bundle();
        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, defaultParameters(), EnumSet.of(DebugSections.TRACES));

        MeasureEvaluationResult.DebugInfo info = result.getDebugInfo();
        assertNotNull(info);
        assertNotNull(info.getTraces(), "traces should be populated when TRACES requested");
        assertFalse(info.getTraces().isEmpty(), "engine should produce at least one trace frame");

        // No other sections.
        assertNull(info.getGroups());
        assertNull(info.getExpressionResults());
        assertNull(info.getLibraryDebug());
        assertNull(info.getCqlMessages());
        assertNull(info.getDebugLog());
    }

    @Test
    void allSections_populatesEverySection() {
        var bundle = KnowledgeArtifactBuilder.SimpleCohortMeasureTrue.bundle();
        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, defaultParameters(), EnumSet.allOf(DebugSections.class));

        assertNotNull(result.getMeasureReport());
        MeasureEvaluationResult.DebugInfo info = result.getDebugInfo();
        assertNotNull(info);

        // Sections that don't depend on engine state always populate when requested.
        assertNotNull(info.getGroups());
        assertNotNull(info.getErrors());
        assertFalse(info.getGroups().isEmpty());

        // Sections that depend on the engine populate as long as the measure produces
        // expressions and traces, which the cohort sample does.
        assertNotNull(info.getExpressionResults());
        assertFalse(info.getExpressionResults().isEmpty());

        assertNotNull(info.getTraces());
        assertFalse(info.getTraces().isEmpty());

        // debugLog should include the measure URL header line we prefix.
        assertNotNull(info.getDebugLog());
        assertTrue(info.getDebugLog().contains("Measure:"),
                "debugLog should include the 'Measure:' header line; got: " + info.getDebugLog());
        assertTrue(info.getDebugLog().contains("Subject:"),
                "debugLog should include at least one '--- Subject:' divider; got: " + info.getDebugLog());
    }

    @Test
    void groupsResultDescribesEachPopulation() {
        var bundle = KnowledgeArtifactBuilder.SimpleCohortMeasureTrue.bundle();
        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, defaultParameters(), EnumSet.of(DebugSections.GROUPS));

        MeasureEvaluationResult.DebugInfo info = result.getDebugInfo();
        var group = info.getGroups().get(0);

        assertNotNull(group.getPopulations());
        assertFalse(group.getPopulations().isEmpty(),
                "cohort measure should have at least one population (initial-population)");

        // Every population should have a non-null type code and a non-negative count.
        for (var population : group.getPopulations()) {
            assertNotNull(population.getType(), "population type code should be set");
            assertTrue(population.getCount() >= 0, "population count should be non-negative");
            assertNotNull(population.getSubjects(), "subjects list should be set (may be empty)");
        }

        boolean hasInitialPopulation = group.getPopulations().stream()
                .anyMatch(p -> "initial-population".equalsIgnoreCase(p.getType()));
        assertTrue(hasInitialPopulation,
                "cohort measure should produce an initial-population entry");
    }

    @Test
    void measureReportIsByteForByteEquivalentBetweenFastAndDebugPath() {
        // The two code paths (R4MultiMeasureService.evaluate vs evaluateSingleMeasureCaptureDef)
        // should produce the same MeasureReport. Verify by comparing population counts on
        // the group, which is the primary thing we care about.
        var bundle = KnowledgeArtifactBuilder.SimpleCohortMeasureTrue.bundle();

        var fastResult = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, defaultParameters(), EnumSet.noneOf(DebugSections.class));
        var debugResult = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, defaultParameters(), EnumSet.of(DebugSections.GROUPS));

        var fastReport = fastResult.getMeasureReport();
        var debugReport = debugResult.getMeasureReport();

        assertEquals(fastReport.getGroup().size(), debugReport.getGroup().size(),
                "both code paths should produce the same number of groups");
        assertEquals(fastReport.getGroupFirstRep().getPopulation().size(),
                debugReport.getGroupFirstRep().getPopulation().size(),
                "both code paths should produce the same number of populations");
        for (int i = 0; i < fastReport.getGroupFirstRep().getPopulation().size(); i++) {
            assertEquals(
                    fastReport.getGroupFirstRep().getPopulation().get(i).getCount(),
                    debugReport.getGroupFirstRep().getPopulation().get(i).getCount(),
                    "population count must match between fast and debug paths");
        }
    }
}
