package com.lantanagroup.link.validation.converters;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.CategoryScope;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class CategoryScopeConverterTest {

    private final CategoryScopeConverter converter = new CategoryScopeConverter(new ObjectMapper());

    @Test
    void roundTrip_nullEntity() {
        // Null entity -> null DB column. Important so LABEL/SUPPRESS rows stay truly null
        // rather than storing the literal string "null".
        assertNull(converter.convertToDatabaseColumn(null));
        assertNull(converter.convertToEntityAttribute(null));
        assertNull(converter.convertToEntityAttribute(""));
        assertNull(converter.convertToEntityAttribute("   "));
    }

    @Test
    void roundTrip_emptyScope() {
        CategoryScope scope = new CategoryScope();
        String json = converter.convertToDatabaseColumn(scope);
        assertNotNull(json);
        CategoryScope back = converter.convertToEntityAttribute(json);
        assertNotNull(back);
        assertTrue(back.isEmpty());
    }

    @Test
    void roundTrip_codeSystemsOnly() {
        CategoryScope scope = new CategoryScope();
        scope.setCodeSystems(List.of(
                "https?://fhir\\.cerner\\.com/.*",
                "https?://open\\.epic\\.com/FHIR/StructureDefinition/.*"));

        String json = converter.convertToDatabaseColumn(scope);
        CategoryScope back = converter.convertToEntityAttribute(json);

        assertNotNull(back);
        assertEquals(scope.getCodeSystems(), back.getCodeSystems());
        assertNull(back.getValueSets());
        assertNull(back.getReferencePaths());
    }

    @Test
    void roundTrip_allAxes() {
        CategoryScope scope = new CategoryScope();
        scope.setCodeSystems(List.of("https?://fhir\\.cerner\\.com/.*"));
        scope.setValueSets(List.of("http://hl7\\.org/fhir/ValueSet/encounter-class"));
        scope.setReferencePaths(List.of("Observation\\.subject"));

        String json = converter.convertToDatabaseColumn(scope);
        CategoryScope back = converter.convertToEntityAttribute(json);

        assertEquals(scope.getCodeSystems(), back.getCodeSystems());
        assertEquals(scope.getValueSets(), back.getValueSets());
        assertEquals(scope.getReferencePaths(), back.getReferencePaths());
    }

    @Test
    void databaseColumnExcludesNullFields() {
        // CategoryScope is @JsonInclude(NON_NULL) so absent fields shouldn't appear in the
        // serialised form. Keeps stored JSON compact and tolerant of future field additions.
        CategoryScope scope = new CategoryScope();
        scope.setCodeSystems(List.of("https?://fhir\\.cerner\\.com/.*"));

        String json = converter.convertToDatabaseColumn(scope);
        assertTrue(json.contains("codeSystems"), "codeSystems must be in JSON: " + json);
        assertTrue(!json.contains("valueSets"), "valueSets must not appear when null: " + json);
        assertTrue(!json.contains("referencePaths"), "referencePaths must not appear when null: " + json);
    }
}
