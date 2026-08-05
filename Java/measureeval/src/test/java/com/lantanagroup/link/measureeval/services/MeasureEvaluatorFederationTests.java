package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import com.github.tomakehurst.wiremock.junit5.WireMockRuntimeInfo;
import com.github.tomakehurst.wiremock.junit5.WireMockTest;
import com.lantanagroup.link.measureeval.models.DebugSections;
import com.lantanagroup.link.measureeval.models.MeasureEvaluationResult;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.CapabilityStatement;
import org.hl7.fhir.r4.model.DateTimeType;
import org.hl7.fhir.r4.model.Enumerations;
import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.StringType;
import org.hl7.fhir.r4.model.ValueSet;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.EnumSet;

import static com.github.tomakehurst.wiremock.client.WireMock.aResponse;
import static com.github.tomakehurst.wiremock.client.WireMock.anyUrl;
import static com.github.tomakehurst.wiremock.client.WireMock.equalTo;
import static com.github.tomakehurst.wiremock.client.WireMock.get;
import static com.github.tomakehurst.wiremock.client.WireMock.getAllServeEvents;
import static com.github.tomakehurst.wiremock.client.WireMock.stubFor;
import static com.github.tomakehurst.wiremock.client.WireMock.urlPathEqualTo;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Exercises the federated terminology wire-up added to {@link MeasureEvaluator}. Uses the same
 * fixtures as {@link MeasureEvaluatorEvaluationTests} — no NHSN bundles, no on-disk data.
 *
 * <p>Four scenarios verify the invariants that matter:
 * <ol>
 *   <li><b>No federation:</b> bundle carries its own ValueSet; report computes as usual.</li>
 *   <li><b>Federation on, bundle self-sufficient:</b> report is identical AND the mock TS
 *       receives zero requests. This is the load-bearing test — if a change to
 *       {@code FederatedFhirRepository} ever calls the remote before consulting the bundle
 *       (or in parallel), this fires. The custom class exists specifically because CQF's
 *       stock {@code FederatedRepository} does <em>not</em> guarantee this — see
 *       {@code MeasureEvaluator.buildRepository()} Javadoc for the rationale.</li>
 *   <li><b>Federation on, bundle missing the VS:</b> mock TS serves the missing VS,
 *       report matches the self-sufficient case.</li>
 *   <li><b>Federation on, remote unreachable:</b> evaluation degrades gracefully to an
 *       empty result (initial-population = 0) rather than throwing.</li>
 * </ol>
 */
@WireMockTest
class MeasureEvaluatorFederationTests {

    private static final String VS_URL = "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307";

    private final FhirContext fhirContext = FhirContext.forR4Cached();

    // ---------- fixture helpers ----------

    /** Rebuilds the CohortMeasureWithValueSetTrue bundle without its embedded ValueSet, forcing
     *  the terminology tier to fall through to the remote. */
    private Bundle bundleWithoutValueSets() {
        var full = KnowledgeArtifactBuilder.CohortMeasureWithValueSetTrue.bundle();
        var stripped = new Bundle();
        stripped.setType(full.getType());
        full.getEntry().stream()
                .filter(e -> !(e.getResource() instanceof ValueSet))
                .forEach(e -> stripped.addEntry(e.copy()));
        return stripped;
    }

    private Parameters evaluateParams() {
        var params = new Parameters();
        params.addParameter().setName("periodStart").setValue(new DateTimeType("2024-01-01"));
        params.addParameter().setName("periodEnd").setValue(new DateTimeType("2024-12-31"));
        params.addParameter().setName("subject").setValue(new StringType("Patient/simple-patient"));
        params.addParameter().setName("additionalData").setResource(PatientDataBuilder.simplePatientAndEncounterBundle());
        return params;
    }

    private IGenericClient clientAgainst(WireMockRuntimeInfo wm) {
        return fhirContext.newRestfulGenericClient(wm.getHttpBaseUrl());
    }

    /**
     * Stubs HAPI's client-bootstrap capability-statement fetch. HAPI's IGenericClient validates
     * the server base URL once per base URL by calling {@code GET /metadata}. Without this stub,
     * WireMock's default 404 response makes the client bootstrap fail before any actual search
     * can run. Tests that exercise the remote path need this stub; scenario 2 (bundle-first,
     * remote never touched) doesn't.
     */
    private void stubMetadata() {
        var cs = new CapabilityStatement();
        cs.setStatus(Enumerations.PublicationStatus.ACTIVE);
        cs.setFhirVersion(Enumerations.FHIRVersion._4_0_1);
        stubFor(get(urlPathEqualTo("/metadata"))
                .willReturn(aResponse()
                        .withStatus(200)
                        .withHeader("Content-Type", "application/fhir+json")
                        .withBody(fhirContext.newJsonParser().encodeResourceToString(cs))));
    }

    /** Stubs a HAPI-style ValueSet search-by-URL response returning the given ValueSet in a searchset bundle. */
    private void stubValueSetSearch(ValueSet vs) {
        var searchset = new Bundle();
        searchset.setType(Bundle.BundleType.SEARCHSET);
        searchset.addEntry().setResource(vs);
        stubFor(get(urlPathEqualTo("/ValueSet"))
                .withQueryParam("url", equalTo(VS_URL))
                .willReturn(aResponse()
                        .withStatus(200)
                        .withHeader("Content-Type", "application/fhir+json")
                        .withBody(fhirContext.newJsonParser().encodeResourceToString(searchset))));
    }

    private int initialPopulationCount(MeasureEvaluationResult result) {
        return result.getMeasureReport().getGroupFirstRep().getPopulationFirstRep().getCount();
    }

    // ---------- scenarios ----------

    @Test
    @DisplayName("1: no federation — bundle-embedded VS is sufficient")
    void noFederation_bundleHasTerminology_producesReport() {
        var bundle = KnowledgeArtifactBuilder.CohortMeasureWithValueSetTrue.bundle();

        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, evaluateParams(), EnumSet.noneOf(DebugSections.class));

        assertNotNull(result.getMeasureReport());
        assertEquals(1, initialPopulationCount(result),
                "initial-population should be 1 (bundle-embedded VS matched the encounter)");
    }

    @Test
    @DisplayName("2: federation on but bundle self-sufficient — mock TS not called")
    void federation_bundleHasTerminology_mockTsNotCalled(WireMockRuntimeInfo wm) {
        var bundle = KnowledgeArtifactBuilder.CohortMeasureWithValueSetTrue.bundle();

        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, evaluateParams(), EnumSet.noneOf(DebugSections.class),
                clientAgainst(wm));

        assertNotNull(result.getMeasureReport());
        assertEquals(1, initialPopulationCount(result),
                "initial-population should still be 1 — federation must not change results when the bundle has the VS");

        var events = getAllServeEvents();
        assertTrue(events.isEmpty(),
                "Bundle contains the VS; the remote TS should not have been consulted. Recorded requests: " + events);
    }

    @Test
    @DisplayName("3: federation on, bundle missing VS — mock TS serves it, same result")
    void federation_bundleMissingTerminology_mockTsCalled_sameReport(WireMockRuntimeInfo wm) {
        var strippedBundle = bundleWithoutValueSets();
        stubMetadata();
        stubValueSetSearch(ValueSetBuilder.inpatientEncounter());

        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, strippedBundle, evaluateParams(), EnumSet.noneOf(DebugSections.class),
                clientAgainst(wm));

        assertNotNull(result.getMeasureReport());
        assertEquals(1, initialPopulationCount(result),
                "initial-population should be 1 — remote-served VS should produce the same result as bundle-embedded");

        assertTrue(
                getAllServeEvents().stream()
                        .anyMatch(e -> e.getRequest().getUrl().startsWith("/ValueSet")),
                "Expected the terminology tier to fall through to the mock TS for the missing VS");
    }

    @Test
    @DisplayName("4: federation on, remote unreachable — evaluation degrades gracefully to empty result")
    void federation_bundleMissingTerminology_mockTsDown_degradesToEmpty(WireMockRuntimeInfo wm) {
        var strippedBundle = bundleWithoutValueSets();
        // Every request (including /metadata) fails. FederatedFhirRepository's search() must catch
        // the failure and return the (empty) local result rather than propagating the exception
        // through the CQL engine.
        stubFor(get(anyUrl()).willReturn(aResponse().withStatus(503)));

        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, strippedBundle, evaluateParams(), EnumSet.noneOf(DebugSections.class),
                clientAgainst(wm));

        assertNotNull(result.getMeasureReport(),
                "Even when remote TS is down, evaluation should complete and produce a MeasureReport");
        assertEquals(0, initialPopulationCount(result),
                "With the VS unresolved (bundle empty, remote failing), the population check has no members — count 0");
    }
}
