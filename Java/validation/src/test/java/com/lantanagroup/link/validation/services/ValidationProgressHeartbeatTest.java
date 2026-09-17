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
    }
}
