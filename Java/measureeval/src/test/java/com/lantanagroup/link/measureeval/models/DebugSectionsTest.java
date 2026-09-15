package com.lantanagroup.link.measureeval.models;

import org.junit.jupiter.api.Test;

import java.util.EnumSet;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class DebugSectionsTest {

    @Test
    void parse_nullOrBlankReturnsEmpty() {
        assertTrue(DebugSections.parse(null).isEmpty());
        assertTrue(DebugSections.parse("").isEmpty());
        assertTrue(DebugSections.parse("   ").isEmpty());
    }

    @Test
    void parse_falseReturnsEmpty() {
        assertTrue(DebugSections.parse("false").isEmpty());
        assertTrue(DebugSections.parse("FALSE").isEmpty());
        assertTrue(DebugSections.parse(" False ").isEmpty());
    }

    @Test
    void parse_trueReturnsAll() {
        assertEquals(EnumSet.allOf(DebugSections.class), DebugSections.parse("true"));
        assertEquals(EnumSet.allOf(DebugSections.class), DebugSections.parse("TRUE"));
        assertEquals(EnumSet.allOf(DebugSections.class), DebugSections.parse("all"));
        assertEquals(EnumSet.allOf(DebugSections.class), DebugSections.parse("ALL"));
    }

    @Test
    void parse_singleSection() {
        assertEquals(EnumSet.of(DebugSections.EXPRESSIONS), DebugSections.parse("expressions"));
        assertEquals(EnumSet.of(DebugSections.LIBRARY_DEBUG), DebugSections.parse("librarydebug"));
        assertEquals(EnumSet.of(DebugSections.DEBUG_LOG), DebugSections.parse("debuglog"));
    }

    @Test
    void parse_commaSeparated() {
        Set<DebugSections> parsed = DebugSections.parse("groups,expressions,traces");
        assertEquals(EnumSet.of(DebugSections.GROUPS, DebugSections.EXPRESSIONS, DebugSections.TRACES), parsed);
    }

    @Test
    void parse_isCaseInsensitiveAndIgnoresWhitespace() {
        Set<DebugSections> parsed = DebugSections.parse(" Groups , EXPRESSIONS , Traces ");
        assertEquals(EnumSet.of(DebugSections.GROUPS, DebugSections.EXPRESSIONS, DebugSections.TRACES), parsed);
    }

    @Test
    void parse_unknownTokensAreIgnored() {
        Set<DebugSections> parsed = DebugSections.parse("groups,unknown,expressions");
        assertEquals(EnumSet.of(DebugSections.GROUPS, DebugSections.EXPRESSIONS), parsed);
    }

    @Test
    void needsDebugLogging_trueForExpressionsLibraryDebugMessagesDebugLog() {
        assertTrue(DebugSections.needsDebugLogging(EnumSet.of(DebugSections.EXPRESSIONS)));
        assertTrue(DebugSections.needsDebugLogging(EnumSet.of(DebugSections.LIBRARY_DEBUG)));
        assertTrue(DebugSections.needsDebugLogging(EnumSet.of(DebugSections.MESSAGES)));
        assertTrue(DebugSections.needsDebugLogging(EnumSet.of(DebugSections.DEBUG_LOG)));
    }

    @Test
    void needsDebugLogging_falseForGroupsAndTracesOnly() {
        assertFalse(DebugSections.needsDebugLogging(EnumSet.noneOf(DebugSections.class)));
        assertFalse(DebugSections.needsDebugLogging(EnumSet.of(DebugSections.GROUPS)));
        assertFalse(DebugSections.needsDebugLogging(EnumSet.of(DebugSections.TRACES)));
        assertFalse(DebugSections.needsDebugLogging(EnumSet.of(DebugSections.GROUPS, DebugSections.TRACES)));
    }

    @Test
    void needsTracing_trueForTracesAndDebugLog() {
        assertTrue(DebugSections.needsTracing(EnumSet.of(DebugSections.TRACES)));
        assertTrue(DebugSections.needsTracing(EnumSet.of(DebugSections.DEBUG_LOG)));
    }

    @Test
    void needsTracing_falseForOtherSectionsAlone() {
        assertFalse(DebugSections.needsTracing(EnumSet.noneOf(DebugSections.class)));
        assertFalse(DebugSections.needsTracing(EnumSet.of(DebugSections.GROUPS)));
        assertFalse(DebugSections.needsTracing(EnumSet.of(DebugSections.EXPRESSIONS)));
        assertFalse(DebugSections.needsTracing(EnumSet.of(DebugSections.LIBRARY_DEBUG)));
        assertFalse(DebugSections.needsTracing(EnumSet.of(DebugSections.MESSAGES)));
    }
}
