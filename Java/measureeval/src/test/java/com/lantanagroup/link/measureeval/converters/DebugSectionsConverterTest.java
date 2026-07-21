package com.lantanagroup.link.measureeval.converters;

import com.lantanagroup.link.measureeval.models.DebugSections;
import org.junit.jupiter.api.Test;

import java.util.EnumSet;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * The converter is intentionally a one-line delegate to {@link DebugSections#parse(String)},
 * so the meaningful behavior is already covered by {@code DebugSectionsTest}. These tests
 * exist as a thin contract check so that if anyone ever changes the delegate to do something
 * else, that change is intentional and visible.
 */
class DebugSectionsConverterTest {

    private final DebugSectionsConverter converter = new DebugSectionsConverter();

    @Test
    void convert_falseReturnsEmpty() {
        assertTrue(converter.convert("false").isEmpty());
    }

    @Test
    void convert_trueReturnsAll() {
        assertEquals(EnumSet.allOf(DebugSections.class), converter.convert("true"));
    }

    @Test
    void convert_commaSeparatedReturnsExactSet() {
        assertEquals(
                EnumSet.of(DebugSections.EXPRESSIONS, DebugSections.TRACES),
                converter.convert("expressions,traces"));
    }

    @Test
    void convert_emptyStringReturnsEmpty() {
        assertTrue(converter.convert("").isEmpty());
    }
}
