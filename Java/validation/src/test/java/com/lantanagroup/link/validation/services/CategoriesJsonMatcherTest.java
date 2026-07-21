package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.CategorySnapshot;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.matchers.Matcher;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;

import java.io.InputStream;
import java.util.Arrays;
import java.util.Map;
import java.util.stream.Collectors;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Exercises the two matchers that previously used an inverted (exclusion) child inside an OR,
 * which made them attach to nearly every result. Loads the shipped categories.json so the test
 * guards the actual data. See LEGLINK-602.
 */
class CategoriesJsonMatcherTest {

    private static final String NO_CODES = "no_codes_from_an_extensible_binding_valueset";
    private static final String UNKNOWN_LOCAL_CODE = "unknown_local_code";

    // Real validation messages observed against a live terminology service.
    private static final String GENUINE_EXTENSIBLE_MISS =
            "None of the codings provided are in the value set 'US Core Encounter Type' "
                    + "(http://hl7.org/fhir/us/core/ValueSet/us-core-encounter-type|6.1.0), and a coding should come "
                    + "from this value set unless it has no suitable code (note that the validator cannot judge what is suitable) "
                    + "(codes = http://snomed.info/sct#109006)";
    private static final String LOINC_GLUMT_EXCLUDED_MISS =
            "None of the codings provided are in the value set 'LOINC Codes' "
                    + "(http://hl7.org/fhir/ValueSet/observation-codes|2.0.1), and a coding should come from this value set "
                    + "unless it has no suitable code (note that the validator cannot judge what is suitable) (codes = http://loinc.org#GLUMT)";
    private static final String IN_MEMORY_EXPANSION_MISS =
            "Unknown code 'http://loinc.org#1234-5' for in-memory expansion of ValueSet 'http://hl7.org/fhir/ValueSet/observation-codes'";
    private static final String VENDOR_OID_EXPANSION_EXCLUDED =
            "Unknown code 'urn:oid:1.2.840.114350.1.72.1.7.7.10.696784.13260#6' for in-memory expansion of ValueSet "
                    + "'http://terminology.hl7.org/ValueSet/v3-ActEncounterCode'";
    private static final String NARRATIVE_WARNING =
            "Constraint failed: dom-6: 'A resource should have narrative for robust management' "
                    + "(defined in http://hl7.org/fhir/StructureDefinition/DomainResource) (Best Practice Recommendation)";
    private static final String STRUCTURE_ERROR =
            "Encounter.location.period: minimum required = 1, but only found 0 "
                    + "(from http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/ach-monthly-encounter|2.0.0-cibuild)";

    private static Map<String, Matcher> matchersById;

    @BeforeAll
    static void loadCategories() throws Exception {
        ObjectMapper objectMapper = new ObjectMapper();
        try (InputStream stream = CategoriesJsonMatcherTest.class.getResourceAsStream("/categories.json")) {
            assertNotNull(stream, "categories.json must be on the classpath");
            CategorySnapshot[] snapshots = objectMapper.readValue(stream, CategorySnapshot[].class);
            matchersById = Arrays.stream(snapshots)
                    .collect(Collectors.toMap(CategorySnapshot::getId, CategorySnapshot::getMatcher));
        }
        assertNotNull(matchersById.get(NO_CODES), NO_CODES + " category must exist");
        assertNotNull(matchersById.get(UNKNOWN_LOCAL_CODE), UNKNOWN_LOCAL_CODE + " category must exist");
    }

    private static boolean matches(String categoryId, String message) {
        Result result = new Result();
        result.setMessage(message);
        return matchersById.get(categoryId).isMatch(result);
    }

    // --- no_codes_from_an_extensible_binding_valueset ---

    @Test
    void noCodes_matchesGenuineExtensibleBindingMiss() {
        assertTrue(matches(NO_CODES, GENUINE_EXTENSIBLE_MISS));
    }

    @Test
    void noCodes_stillExcludesLoincGlumtCase() {
        assertFalse(matches(NO_CODES, LOINC_GLUMT_EXCLUDED_MISS));
    }

    @Test
    void noCodes_doesNotMatchNarrativeWarning() {
        assertFalse(matches(NO_CODES, NARRATIVE_WARNING));
    }

    @Test
    void noCodes_doesNotMatchStructureError() {
        assertFalse(matches(NO_CODES, STRUCTURE_ERROR));
    }

    // --- unknown_local_code ---

    @Test
    void unknownLocalCode_matchesInMemoryExpansionMiss() {
        assertTrue(matches(UNKNOWN_LOCAL_CODE, IN_MEMORY_EXPANSION_MISS));
    }

    @Test
    void unknownLocalCode_stillExcludesVendorOidExpansion() {
        assertFalse(matches(UNKNOWN_LOCAL_CODE, VENDOR_OID_EXPANSION_EXCLUDED));
    }

    @Test
    void unknownLocalCode_doesNotMatchNarrativeWarning() {
        assertFalse(matches(UNKNOWN_LOCAL_CODE, NARRATIVE_WARNING));
    }

    @Test
    void unknownLocalCode_doesNotMatchStructureError() {
        assertFalse(matches(UNKNOWN_LOCAL_CODE, STRUCTURE_ERROR));
    }
}
