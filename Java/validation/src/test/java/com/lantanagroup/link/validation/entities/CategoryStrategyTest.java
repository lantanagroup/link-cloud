package com.lantanagroup.link.validation.entities;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

/**
 * The enum is trivial; these tests exist only to lock the literal values that get
 * persisted in the {@code category.strategy} column. Renaming a value would silently
 * break stored data — these tests force a deliberate, visible change in that case.
 */
class CategoryStrategyTest {

    @Test
    void literalValues() {
        assertEquals(3, CategoryStrategy.values().length);
        assertEquals("SKIP", CategoryStrategy.SKIP.name());
        assertEquals("SUPPRESS", CategoryStrategy.SUPPRESS.name());
        assertEquals("LABEL", CategoryStrategy.LABEL.name());
    }

    @Test
    void valueOf_caseSensitive() {
        assertEquals(CategoryStrategy.LABEL, CategoryStrategy.valueOf("LABEL"));
        assertThrows(IllegalArgumentException.class, () -> CategoryStrategy.valueOf("label"));
        assertThrows(IllegalArgumentException.class, () -> CategoryStrategy.valueOf("Unknown"));
    }
}
