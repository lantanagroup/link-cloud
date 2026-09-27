package com.lantanagroup.link.validation.services;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class ValidationProgressHeartbeatTest {
    @Test
    void format_includes_token_detail_and_elapsed() {
        String line = ValidationProgressHeartbeat.format("FHIR bundle 11781 entries", 90);
        assertEquals("validation still in progress: FHIR bundle 11781 entries (elapsed 90s)", line);
        assertTrue(line.contains(ValidationProgressHeartbeat.LOG_TOKEN));

        String scoped = ValidationProgressHeartbeat.format(
                "FHIR bundle 11781 entries",
                "fac-1",
                "rep-1",
                90);
        assertEquals(
                "validation still in progress: FHIR bundle 11781 entries facility=fac-1 report=rep-1 (elapsed 90s)",
                scoped);
    }

    @Test
    void format_replaces_control_characters_in_ids() {
        String line = ValidationProgressHeartbeat.format(
                "FHIR bundle 1 entries",
                "fac\n1",
                "rep\r2",
                1);
        assertEquals(
                "validation still in progress: FHIR bundle 1 entries facility=fac 1 report=rep 2 (elapsed 1s)",
                line);
    }
}
