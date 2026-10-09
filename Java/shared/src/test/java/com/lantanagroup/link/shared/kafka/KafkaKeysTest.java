package com.lantanagroup.link.shared.kafka;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashSet;
import java.util.Set;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class KafkaKeysTest {

    @Test
    void forPatientJoinsFacilityAndPatient() {
        assertEquals(
                "{\"facilityId\":\"  facility-1  \",\"patientId\":\" patient-9 \"}",
                KafkaKeys.forPatient("  facility-1  ", " patient-9 "));
    }

    @ParameterizedTest
    @CsvSource(value = {
            "null,patient",
            "'',patient",
            "facility,null",
            "facility,''"
    }, nullValues = "null")
    void forPatientRejectsEmptyParts(String facilityId, String patientId) {
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forPatient(facilityId, patientId));
    }

    @Test
    void forFacilityKeepsSpacesAndRejectsEmpty() {
        assertEquals("{\"facilityId\":\" facility-1 \"}", KafkaKeys.forFacility(" facility-1 "));
        assertEquals("{\"facilityId\":\" \"}", KafkaKeys.forFacility(" "));
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forFacility(""));
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forFacility(null));
    }

    @Test
    void forServiceKeepsSpacesAndRejectsEmpty() {
        assertEquals(" measureeval ", KafkaKeys.forService(" measureeval "));
        assertEquals(" ", KafkaKeys.forService(" "));
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forService(""));
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forService(null));
    }

    @Test
    void forAuditUsesPatientThenFacilityThenService() {
        assertEquals(
                "{\"facilityId\":\"fac\",\"patientId\":\"pat\"}",
                KafkaKeys.forAudit("fac", "pat", "QueryDispatch"));
        assertEquals("{\"facilityId\":\"fac\"}", KafkaKeys.forAudit("fac", null, "QueryDispatch"));
        assertEquals("QueryDispatch", KafkaKeys.forAudit(null, "", "QueryDispatch"));
        assertEquals("{\"facilityId\":\" \"}", KafkaKeys.forAudit(" ", null, "QueryDispatch"));
    }

    @Test
    void rejectsUnpairedSurrogatesAndKeepsPairs() {
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forPatient("\uD800", "patient"));
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forPatient("facility", "\uDFFF"));
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forPatient("a\uD800b", "patient"));
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forPatient("ok\uD83D", "patient"));
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forFacility("\uDC00"));
        assertThrows(IllegalArgumentException.class, () -> KafkaKeys.forService("\uD800"));
        assertEquals(
                "{\"facilityId\":\"\uD83D\uDE00\",\"patientId\":\"patient-proof\"}",
                KafkaKeys.forPatient("\uD83D\uDE00", "patient-proof"));
    }

    @Test
    void serviceNameKeyIsNotAFacility() {
        assertNull(KafkaKeyLegacy.tryReadFacility("Audit"));
        assertNull(KafkaKeyLegacy.tryReadFacility("  QueryDispatch  "));
        assertNull(KafkaIdentity.facility(null, "DataAcquisitionWorker"));
        assertNull(KafkaKeyLegacy.tryReadFacility("measureeval"));
        assertNull(KafkaKeyLegacy.tryReadFacility("ValidationService"));
        assertEquals("facility-1", KafkaIdentity.facility(null, "facility-1"));
        assertEquals("Audit", KafkaKeyLegacy.tryReadFacility("{\"facilityId\":\"Audit\"}"));
    }

    @Test
    void serviceNamesComeFromTheSharedFixture() throws Exception {
        Path root = Path.of("").toAbsolutePath();
        while (root != null && !Files.exists(root.resolve("topics.txt"))) {
            root = root.getParent();
        }
        if (root == null) {
            throw new IllegalStateException("Could not find the repository root.");
        }
        Path tests = childIgnoreCase(root, "tests");
        Path fixtures = childIgnoreCase(tests, "fixtures");
        Path file;
        try (var files = Files.list(fixtures)) {
            file = files
                    .filter(path -> path.getFileName().toString().equalsIgnoreCase("kafka-service-names.json"))
                    .findFirst()
                    .orElseThrow();
        }
        JsonNode names = new ObjectMapper().readTree(Files.readString(file));
        assertTrue(names.isArray());
        Set<String> fromFile = new HashSet<>();
        for (JsonNode name : names) {
            assertTrue(fromFile.add(name.asText()));
            assertNull(KafkaKeyLegacy.tryReadFacility(name.asText()));
        }
        assertEquals(fromFile, KafkaKeyLegacy.knownServiceNames());
    }

    private static Path childIgnoreCase(Path parent, String name) throws Exception {
        try (var children = Files.list(parent)) {
            return children
                    .filter(path -> Files.isDirectory(path) && path.getFileName().toString().equalsIgnoreCase(name))
                    .findFirst()
                    .orElseThrow();
        }
    }

    @Test
    void canonicalKeyEscapesQuotesSlashesAndKeepsNonAscii() {
        assertEquals(
                "{\"facilityId\":\"a/b\\\"c\\\\d\",\"patientId\":\"患者\"}",
                KafkaKeys.forPatient("a/b\"c\\d", "患者"));
    }

    @Test
    void readsLegacyResourceKey() {
        String key = "{\"facilityId\":\"fac\",\"patientId\":\"pat\"}";
        assertEquals("fac", KafkaKeyLegacy.tryReadFacility(key));
        assertEquals("pat", KafkaKeyLegacy.tryReadPatient(key));
    }

    @Test
    void readsLegacyReportKeyIgnoringCase() {
        UUID reportId = UUID.fromString("11111111-1111-1111-1111-111111111111");
        String key = "{\"FacilityId\":\"fac\",\"ReportScheduleId\":\"" + reportId + "\"}";
        assertEquals(reportId, KafkaKeyLegacy.tryReadReportScheduleId(key));
        assertEquals("fac", KafkaIdentity.facility(null, key));
        assertEquals(reportId, KafkaIdentity.reportSchedule(null, key));
    }

    @Test
    void plainFacilityKeyIsLegacy() {
        assertEquals("fac", KafkaKeyLegacy.tryReadFacility("  fac  "));
        assertNull(KafkaKeyLegacy.tryReadPatient("fac"));
        assertNull(KafkaKeyLegacy.tryReadReportScheduleId("fac"));
        assertEquals("fac", KafkaIdentity.facility(null, "fac"));
    }

    @Test
    void valueWinsOverLegacyKey() {
        assertEquals("from-value", KafkaIdentity.facility("from-value", "{\"facilityId\":\"from-key\"}"));
        assertEquals("from-value", KafkaIdentity.patient("from-value", "{\"patientId\":\"from-key\"}"));
        UUID fromValue = UUID.fromString("22222222-2222-2222-2222-222222222222");
        UUID fromKey = UUID.fromString("11111111-1111-1111-1111-111111111111");
        assertEquals(fromValue, KafkaIdentity.reportSchedule(fromValue,
                "{\"reportScheduleId\":\"" + fromKey + "\"}"));
    }

    @Test
    void currentColonKeyIsNotParsed() {
        assertNull(KafkaKeyLegacy.tryReadFacility("fac:pat"));
        assertNull(KafkaKeyLegacy.tryReadPatient("fac:pat"));
        assertNull(KafkaKeyLegacy.tryReadReportScheduleId("fac:pat"));
        assertNull(KafkaIdentity.facility(null, "fac:pat"));
        assertNull(KafkaIdentity.patient(" ", "fac:pat"));
    }

    @Test
    void legacyReaderRejectsBlankEmptyAndNonStringProperties() {
        assertNull(KafkaKeyLegacy.tryReadFacility(null));
        assertNull(KafkaKeyLegacy.tryReadFacility("   "));
        assertNull(KafkaKeyLegacy.tryReadFacility("{"));
        // No colon, so a non-object is the legacy plain facility id.
        assertEquals("[]", KafkaKeyLegacy.tryReadFacility("[]"));
        assertNull(KafkaKeyLegacy.tryReadFacility("{\"facilityId\":\"   \",\"patientId\":1}"));
        assertNull(KafkaKeyLegacy.tryReadPatient("{\"patientId\":\"  \"}"));
        assertNull(KafkaKeyLegacy.tryReadReportScheduleId("{\"reportScheduleId\":\"00000000-0000-0000-0000-000000000000\"}"));
        assertNull(KafkaKeyLegacy.tryReadReportScheduleId("{\"reportScheduleId\":\"not-a-guid\"}"));
    }
}
