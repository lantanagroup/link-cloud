package com.lantanagroup.link.validation.converters;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;

class SuppressMessageIdsConverterTest {

    private final SuppressMessageIdsConverter converter = new SuppressMessageIdsConverter(new ObjectMapper());

    @Test
    void roundTrip_nullEntity() {
        // Null entity -> null DB column. Keeps non-SUPPRESS rows genuinely empty rather than
        // storing the literal string "null" or "[]".
        assertNull(converter.convertToDatabaseColumn(null));
        assertNull(converter.convertToEntityAttribute(null));
        assertNull(converter.convertToEntityAttribute(""));
        assertNull(converter.convertToEntityAttribute("   "));
    }

    @Test
    void roundTrip_emptyList() {
        // Empty list -> null DB column too. Storing "[]" would suggest the rule was authored
        // with SUPPRESS but explicitly empty, which has no semantic difference from "no SUPPRESS"
        // and would just create needless attribution noise downstream.
        assertNull(converter.convertToDatabaseColumn(List.of()));
    }

    @Test
    void roundTrip_singleId() {
        List<String> input = List.of("Reference_REF_NoDisplay");
        String json = converter.convertToDatabaseColumn(input);
        assertNotNull(json);
        List<String> back = converter.convertToEntityAttribute(json);
        assertEquals(input, back);
    }

    @Test
    void roundTrip_multipleIds() {
        List<String> input = List.of(
                "Terminology_TX_System_Unknown",
                "Coding_has_no_system__cannot_validate",
                "TERMINOLOGY_TX_SYSTEM_NO_CODE");
        String json = converter.convertToDatabaseColumn(input);
        List<String> back = converter.convertToEntityAttribute(json);
        assertEquals(input, back);
    }

    @Test
    void roundTrip_typeReferencePreservesStringElements() {
        // Critical: deserialized list elements must be String, not Object. The TypeReference is
        // what guarantees this — passing List.class would yield List<Object> with String values.
        // If someone replaces the TypeReference with a raw Class<List>, this test catches it.
        List<String> input = List.of("FirstId", "SecondId");
        String json = converter.convertToDatabaseColumn(input);
        List<String> back = converter.convertToEntityAttribute(json);

        for (Object element : back) {
            assertEquals(String.class, element.getClass(),
                    "Deserialized elements must be String — TypeReference<List<String>> is load-bearing");
        }
    }
}
