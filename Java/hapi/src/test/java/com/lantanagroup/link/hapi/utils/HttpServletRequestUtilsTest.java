package com.lantanagroup.link.hapi.utils;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;

class HttpServletRequestUtilsTest {
    @Test
    void neutralizesLogEntryAndTerminalControlCharacters() {
        assertEquals("client__forged_[31m___",
                HttpServletRequestUtils.sanitizeForLog("client\r\nforged\u001b[31m\u0085\u2028\u2029"));
    }

    @Test
    void preservesIpv4Ipv6AndOrdinaryDiagnosticText() {
        assertEquals("2001:db8::1", HttpServletRequestUtils.sanitizeForLog("2001:db8::1"));
        assertEquals("192.0.2.1", HttpServletRequestUtils.sanitizeForLog("192.0.2.1"));
        assertNull(HttpServletRequestUtils.sanitizeForLog(null));
    }
}
