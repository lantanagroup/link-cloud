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
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Exercises the federated terminology wire-up added to {@link MeasureEvaluator}. Uses the same
 * fixtures as {@link MeasureEvaluatorEvaluationTests} — no NHSN bundles, no on-disk data.
 *
 * <p>Four scenarios verify the invariants that matter under the TS-authoritative model:
 * <ol>
 *   <li><b>No TS configured:</b> bundle carries its own ValueSet; report computes from the
 *       bundle.</li>
 *   <li><b>TS configured, bundle also has the VS:</b> the mock TS is consulted (even though the
 *       bundle has a copy) and its response is used. The TS-authoritative invariant lives here
 *       — if a change to {@code FederatedFhirRepository} ever short-circuits back to the bundle
 *       for terminology, this fires. The custom class exists specifically to guarantee this
 *       routing — see {@code MeasureEvaluator.buildRepository()} Javadoc.</li>
 *   <li><b>TS configured, bundle missing the VS:</b> mock TS serves the VS, report matches the
 *       no-TS case.</li>
 *   <li><b>TS configured, remote unreachable:</b> the failure propagates — evaluation throws
 *       rather than silently falling back to any bundle-embedded expansions.</li>
 * </ol>
 */
@WireMockTest
class MeasureEvaluatorFederationTests {

    private static final String VS_URL = "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307";

    private final FhirContext fhirContext = FhirContext.forR4Cached();

    // ---------- fixture helpers ----------

    /** Rebuilds the CohortMeasureWithValueSetTrue bundle without its embedded ValueSet. */
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
     * can run.
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
    @DisplayName("1: no TS — bundle-embedded VS is used directly")
    void noTs_bundleHasTerminology_producesReport() {
        var bundle = KnowledgeArtifactBuilder.CohortMeasureWithValueSetTrue.bundle();

        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, evaluateParams(), EnumSet.noneOf(DebugSections.class));

        assertNotNull(result.getMeasureReport());
        assertEquals(1, initialPopulationCount(result),
                "initial-population should be 1 (bundle-embedded VS matched the encounter)");
    }

    @Test
    @DisplayName("2: TS configured, bundle also has VS — TS is consulted (TS is authoritative)")
    void ts_bundleAlsoHasTerminology_mockTsIsCalled(WireMockRuntimeInfo wm) {
        var bundle = KnowledgeArtifactBuilder.CohortMeasureWithValueSetTrue.bundle();
        stubMetadata();
        stubValueSetSearch(ValueSetBuilder.inpatientEncounter());

        var result = MeasureEvaluator.compileAndEvaluate(
                fhirContext, bundle, evaluateParams(), EnumSet.noneOf(DebugSections.class),
                clientAgainst(wm));

        assertNotNull(result.getMeasureReport());
        assertEquals(1, initialPopulationCount(result),
                "initial-population should be 1 — TS returned the same VS content the bundle carries");

        assertTrue(
                getAllServeEvents().stream()
                        .anyMatch(e -> e.getRequest().getUrl().startsWith("/ValueSet")),
                "TS is authoritative — the mock TS must have been consulted for the ValueSet even though the bundle also has it");
    }

    @Test
    @DisplayName("3: TS configured, bundle missing VS — TS serves it, same result")
    void ts_bundleMissingTerminology_mockTsCalled_sameReport(WireMockRuntimeInfo wm) {
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
                "Expected the terminology tier to consult the mock TS for the VS");
    }

    @Test
    @DisplayName("4: TS configured, remote unreachable — evaluation propagates the failure")
    void ts_remoteUnreachable_propagatesFailure(WireMockRuntimeInfo wm) {
        var bundle = KnowledgeArtifactBuilder.CohortMeasureWithValueSetTrue.bundle();
        // Every request fails (including /metadata). Under TS-authoritative semantics the
        // failure must propagate rather than silently degrading to bundle-embedded expansions.
        stubFor(get(anyUrl()).willReturn(aResponse().withStatus(503)));

        assertThrows(Exception.class,
                () -> MeasureEvaluator.compileAndEvaluate(
                        fhirContext, bundle, evaluateParams(), EnumSet.noneOf(DebugSections.class),
                        clientAgainst(wm)),
                "Remote TS is down; evaluation must not silently fall back to the bundle's ValueSets");
    }
}
