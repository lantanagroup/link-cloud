package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.r4.model.Base;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Encounter;
import org.hl7.fhir.r4.model.Observation;
import org.hl7.fhir.r4.model.Patient;
import org.hl7.fhir.r4.model.Resource;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * HAPI locates a finding by the entry's position in the bundle it validated
 * ({@code Bundle.entry[7].resource.ofType(Observation).code}). A position identifies nothing to a reader
 * of the submitted NDJSON, which carries bare resources and no bundle, and it silently names a different
 * record if the entries are ever read in another order. These pin the id into every expression - the form
 * used in the MVP - and, just as important, pin every case where the input must be returned untouched: an
 * expression that resolves to nothing is worse than one that is merely positional. LEGLINK-1495.
 */
class InstanceAnchoredExpressionTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();

    private static Bundle bundleOf(Resource... resources) {
        Bundle bundle = new Bundle();
        for (Resource resource : resources) {
            bundle.addEntry().setResource(resource);
        }
        return bundle;
    }

    private static Observation observation(String id) {
        Observation observation = new Observation();
        observation.setId(id);
        return observation;
    }

    private static List<Base> evaluate(Bundle bundle, String expression) {
        return FHIR_CONTEXT.newFhirPath().evaluate(bundle, expression, Base.class);
    }

    @Test
    void insertsTheIdAfterTheOfTypePrefix() {
        Bundle bundle = bundleOf(observation("obs-1"), observation("obs-2"));

        assertEquals(
                "Bundle.entry[1].resource.ofType(Observation).where(id = 'obs-2').code",
                InstanceAnchoredExpression.anchor(
                        "Bundle.entry[1].resource.ofType(Observation).code", bundle));
    }

    @Test
    void keepsEverythingAfterThePrefixIncludingElementIndices() {
        // Element indices stay: a FHIR element has no id, so .component[0] cannot be anchored and must
        // survive verbatim or two findings on one resource collapse into the same pointer.
        Bundle bundle = bundleOf(observation("obs-1"));

        assertEquals(
                "Bundle.entry[0].resource.ofType(Observation).where(id = 'obs-1')"
                        + ".component[0].code.coding[2].system",
                InstanceAnchoredExpression.anchor(
                        "Bundle.entry[0].resource.ofType(Observation).component[0].code.coding[2].system",
                        bundle));
    }

    @Test
    void distinctResourcesYieldDistinctExpressions() {
        // The point of the change: two findings at the same path on different records must stay
        // distinguishable by something the reader can resolve.
        Bundle bundle = bundleOf(observation("obs-1"), observation("obs-2"));

        String first = InstanceAnchoredExpression.anchor(
                "Bundle.entry[0].resource.ofType(Observation).code", bundle);
        String second = InstanceAnchoredExpression.anchor(
                "Bundle.entry[1].resource.ofType(Observation).code", bundle);

        assertNotEquals(first, second);
        assertTrue(first.contains("where(id = 'obs-1')"), first);
        assertTrue(second.contains("where(id = 'obs-2')"), second);
    }

    @Test
    void everyExpressionCarriesAnId() {
        // Not only the first. Each location is independently resolvable to its own record.
        Bundle bundle = bundleOf(observation("obs-1"), observation("obs-2"), observation("obs-3"));

        for (int index = 0; index < 3; index++) {
            String anchored = InstanceAnchoredExpression.anchor(
                    "Bundle.entry[" + index + "].resource.ofType(Observation).code", bundle);
            assertTrue(anchored.contains("where(id = 'obs-" + (index + 1) + "')"), anchored);
        }
    }

    @Test
    void namesTheSameRecordWhateverPositionItOccupies() {
        // The entry index differs between the two bundles, so the strings differ - but both name obs-2,
        // which is what a reader needs and what the position alone could never give.
        Observation first = observation("obs-1");
        Observation second = observation("obs-2");

        String fromOriginal = InstanceAnchoredExpression.anchor(
                "Bundle.entry[1].resource.ofType(Observation).code", bundleOf(first, second));
        String fromReordered = InstanceAnchoredExpression.anchor(
                "Bundle.entry[0].resource.ofType(Observation).code", bundleOf(second, first));

        assertTrue(fromOriginal.contains("where(id = 'obs-2')"), fromOriginal);
        assertTrue(fromReordered.contains("where(id = 'obs-2')"), fromReordered);
    }

    @Test
    void anchorsResourceTypesOtherThanObservation() {
        Patient patient = new Patient();
        patient.setId("pat-1");
        Encounter encounter = new Encounter();
        encounter.setId("enc-1");
        Bundle bundle = bundleOf(patient, encounter);

        assertEquals(
                "Bundle.entry[1].resource.ofType(Encounter).where(id = 'enc-1').location[0].period",
                InstanceAnchoredExpression.anchor(
                        "Bundle.entry[1].resource.ofType(Encounter).location[0].period", bundle));
    }

    // -------------------------------------------------------------------------
    // Evaluated as FHIRPath, against the real engine
    // -------------------------------------------------------------------------

    @Test
    void anchoredExpressionSelectsTheIntendedRecord() {
        Bundle bundle = bundleOf(observation("obs-1"), observation("obs-2"));
        String anchored = InstanceAnchoredExpression.anchor(
                "Bundle.entry[1].resource.ofType(Observation)", bundle);

        List<Base> selected = evaluate(bundle, anchored);

        assertEquals(1, selected.size(), anchored);
        assertEquals("obs-2", ((Observation) selected.get(0)).getIdElement().getIdPart());
    }

    @Test
    void theFilterPropertyIsIdNotResourceId() {
        // Settles which property the filter names, which depends on where it sits. After
        // .resource.ofType(T) the cursor is the resource itself, so the property is `id`; `resource.id`
        // there would mean the resource's own `resource` child, which no resource has. On the entry,
        // before .resource, it is the other way round.
        Bundle bundle = bundleOf(observation("obs-1"));

        assertEquals(1, evaluate(bundle,
                "Bundle.entry[0].resource.ofType(Observation).where(id = 'obs-1')").size(),
                "id, after .resource.ofType(T)");
        assertEquals(1, evaluate(bundle,
                "Bundle.entry[0].where(resource.id = 'obs-1')").size(),
                "resource.id, on the entry");

        List<Base> wrongProperty;
        try {
            wrongProperty = evaluate(bundle,
                    "Bundle.entry[0].resource.ofType(Observation).where(resource.id = 'obs-1')");
        } catch (RuntimeException e) {
            // The engine rejecting it outright is an acceptable outcome too - either way it never
            // selects the record.
            return;
        }
        assertTrue(wrongProperty.isEmpty(),
                "resource.id after .resource.ofType(T) must not select the record");
    }

    @Test
    void anchoredExpressionSelectsNothingOnceTheEntriesHaveMoved() {
        // Self-checking, and the whole reason the index is kept rather than trusted: where(id = ...)
        // filters out whatever now sits at that position, so a stale expression yields an empty result
        // instead of a confident pointer at the wrong record. Empty is detectable; wrong is not.
        Observation first = observation("obs-1");
        Observation second = observation("obs-2");
        String anchored = InstanceAnchoredExpression.anchor(
                "Bundle.entry[1].resource.ofType(Observation)", bundleOf(first, second));

        List<Base> selected = evaluate(bundleOf(second, first), anchored);

        assertTrue(selected.isEmpty(),
                "A moved record must make the expression select nothing, not the wrong record: " + anchored);
    }

    @Test
    void positionalExpressionSelectsTheWrongRecordOnceTheEntriesHaveMoved() {
        // The defect being removed, stated as a test: without the id the expression still resolves, still
        // satisfies ofType(), and names a different Observation with nothing to indicate it.
        Observation first = observation("obs-1");
        Observation second = observation("obs-2");
        String positional = "Bundle.entry[1].resource.ofType(Observation)";

        List<Base> selected = evaluate(bundleOf(second, first), positional);

        assertEquals(1, selected.size());
        assertEquals("obs-1", ((Observation) selected.get(0)).getIdElement().getIdPart(),
                "Positional expression resolves cleanly to the wrong record");
    }

    // -------------------------------------------------------------------------
    // Every case below must return the input unchanged rather than guess
    // -------------------------------------------------------------------------

    @Test
    void leavesAnExpressionWithNoEntryPrefixAlone() {
        Bundle bundle = bundleOf(observation("obs-1"));

        assertEquals("Observation.code",
                InstanceAnchoredExpression.anchor("Observation.code", bundle));
        assertEquals("Bundle.type",
                InstanceAnchoredExpression.anchor("Bundle.type", bundle));
    }

    @Test
    void leavesAnEntryPrefixWithNoOfTypeAlone() {
        // There is nowhere to put the filter without ofType(), and ids are unique only per resource type.
        Bundle bundle = bundleOf(observation("obs-1"));
        String noOfType = "Bundle.entry[0].resource.code";

        assertEquals(noOfType, InstanceAnchoredExpression.anchor(noOfType, bundle));
    }

    @Test
    void leavesAnAlreadyAnchoredExpressionAlone() {
        Bundle bundle = bundleOf(observation("obs-1"));
        String anchored = "Bundle.entry[0].resource.ofType(Observation).where(id = 'obs-1').code";

        assertEquals(anchored, InstanceAnchoredExpression.anchor(anchored, bundle));
    }

    @Test
    void returnsInputWhenTheIndexIsPastTheEndOfTheBundle() {
        Bundle bundle = bundleOf(observation("obs-1"));
        String positional = "Bundle.entry[9].resource.ofType(Observation).code";

        assertEquals(positional, InstanceAnchoredExpression.anchor(positional, bundle));
    }

    @Test
    void returnsInputWhenTheEntryHasNoResource() {
        Bundle bundle = new Bundle();
        bundle.addEntry();
        String positional = "Bundle.entry[0].resource.ofType(Observation).code";

        assertEquals(positional, InstanceAnchoredExpression.anchor(positional, bundle));
    }

    @Test
    void returnsInputWhenTheResourceCarriesNoId() {
        // where(id = 'null') is valid FHIRPath that selects nothing - a silently dead pointer.
        Bundle bundle = bundleOf(new Observation());
        String positional = "Bundle.entry[0].resource.ofType(Observation).code";

        assertEquals(positional, InstanceAnchoredExpression.anchor(positional, bundle));
    }

    @Test
    void returnsInputWhenTheIdWouldNotSurviveQuoting() {
        // A FHIR id is [A-Za-z0-9-.]{1,64}. Anything else - a quote above all - would break the
        // expression it is spliced into, so the positional form is kept instead.
        Bundle bundle = bundleOf(observation("obs'1"));
        String positional = "Bundle.entry[0].resource.ofType(Observation).code";

        assertEquals(positional, InstanceAnchoredExpression.anchor(positional, bundle));
    }

    @Test
    void toleratesNullInputs() {
        assertNull(InstanceAnchoredExpression.anchor(null, bundleOf(observation("obs-1"))));
        assertEquals("Bundle.entry[0].resource.ofType(Observation).code",
                InstanceAnchoredExpression.anchor(
                        "Bundle.entry[0].resource.ofType(Observation).code", null));
    }
}
