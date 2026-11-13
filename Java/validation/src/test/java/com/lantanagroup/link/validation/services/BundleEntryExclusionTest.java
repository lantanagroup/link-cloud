package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.providers.ValidationCacheService;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Observation;
import org.hl7.fhir.r4.model.Patient;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Leaving resource types out of legacy validation ({@code link.validation-excluded-resource-types}): the excluded
 * entries aren't validated, and every finding still names its entry's position in the bundle as submitted.
 */
class BundleEntryExclusionTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();
    private final ExecutorService executor = Executors.newFixedThreadPool(2);

    @AfterEach
    void tearDown() {
        executor.shutdownNow();
    }

    /** Patient, MeasureReport, Observation: entries 0, 1 and 2. The MeasureReport and Observation lack required elements. */
    private static Bundle patientBundle() {
        Bundle bundle = new Bundle();
        bundle.setType(Bundle.BundleType.COLLECTION);
        bundle.addEntry().setFullUrl("urn:uuid:11111111-1111-1111-1111-111111111111").setResource(new Patient().setId("p1"));
        bundle.addEntry().setFullUrl("urn:uuid:22222222-2222-2222-2222-222222222222").setResource(new MeasureReport().setId("mr1"));
        bundle.addEntry().setFullUrl("urn:uuid:33333333-3333-3333-3333-333333333333").setResource(new Observation().setId("o1"));
        return bundle;
    }

    @Test
    void leavesOutTheExcludedTypesIgnoringCase_andKeepsTheOriginalBundleWhole() {
        Bundle bundle = patientBundle();

        BundleEntryExclusion exclusion = BundleEntryExclusion.of(bundle, List.of("measurereport", " ", "Device"));

        assertTrue(exclusion.excludedAny());
        assertEquals(Map.of("MeasureReport", 1), exclusion.skipped());
        assertEquals(List.of("Patient", "Observation"),
                exclusion.bundle().getEntry().stream().map(e -> e.getResource().fhirType()).toList());
        assertEquals(3, bundle.getEntry().size(), "the original bundle is left as it was");
        assertSame(bundle.getEntry().get(2), exclusion.bundle().getEntry().get(1), "entries are shared, not copied");
    }

    @Test
    void mapsALeadingEntryIndexBackToTheOriginalPosition() {
        BundleEntryExclusion exclusion = BundleEntryExclusion.of(patientBundle(), List.of("MeasureReport"));

        assertEquals("Bundle.entry[2].resource.ofType(Observation).status",
                exclusion.toOriginalPath("Bundle.entry[1].resource.ofType(Observation).status"));
        assertEquals("Bundle.entry[0]", exclusion.toOriginalPath("Bundle.entry[0]"));
        assertEquals("Bundle.type", exclusion.toOriginalPath("Bundle.type"));
        assertNull(exclusion.toOriginalPath(null));
    }

    @Test
    void withNothingToLeaveOut_theOriginalBundleIsUsedAsIs() {
        Bundle bundle = patientBundle();

        BundleEntryExclusion exclusion = BundleEntryExclusion.of(bundle, null);

        assertFalse(exclusion.excludedAny());
        assertSame(bundle, exclusion.bundle());
        assertEquals("Bundle.entry[1].x", exclusion.toOriginalPath("Bundle.entry[1].x"));
    }

    private ValidationService service(List<String> excludedTypes) {
        ArtifactRepository artifactRepository = mock(ArtifactRepository.class);
        when(artifactRepository.findAll()).thenReturn(List.of());
        LinkConfig linkConfig = mock(LinkConfig.class);
        when(linkConfig.getWhiteListCodeSystemRegex()).thenReturn(new ArrayList<>());
        when(linkConfig.getWhiteListValueSetRegex()).thenReturn(new ArrayList<>());
        when(linkConfig.getValidationExcludedResourceTypes()).thenReturn(excludedTypes);
        return new ValidationService(FHIR_CONTEXT, new ArtifactService(FHIR_CONTEXT, artifactRepository, linkConfig),
                linkConfig, mock(ValidationCacheService.class), new ValidationResultIgnoreService(mock(LinkConfig.class)),
                executor);
    }

    private static boolean anyAt(List<Result> results, String entryPrefix) {
        return results.stream().anyMatch(r -> r.getLocation() != null && r.getLocation().startsWith(entryPrefix)
                || r.getExpression() != null && r.getExpression().startsWith(entryPrefix));
    }

    @Test
    void validation_leavesTheMeasureReportOut_andReportsTheObservationAtItsSubmittedPosition() {
        List<Result> all = service(List.of()).validate(patientBundle());
        assertTrue(anyAt(all, "Bundle.entry[1]"), "without the setting, the MeasureReport (entry 1) is validated: " + all);
        assertTrue(anyAt(all, "Bundle.entry[2]"), "and the Observation is reported at entry 2");

        List<Result> excluded = service(List.of("MeasureReport")).validate(patientBundle());
        assertFalse(anyAt(excluded, "Bundle.entry[1]"), "the MeasureReport is left out: " + excluded);
        assertTrue(anyAt(excluded, "Bundle.entry[2]"), "the Observation keeps its submitted position, entry 2: " + excluded);
    }
}
