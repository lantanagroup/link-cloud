package com.lantanagroup.link.shared.kafka;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.io.InputStream;
import java.util.HashSet;
import java.util.Iterator;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

// TODO: remove legacy key fallback after one release once producers write ids on the value.
public final class KafkaKeyLegacy {

    private static final ObjectMapper MAPPER = new ObjectMapper();
    private static final UUID NIL_UUID = new UUID(0L, 0L);
    // Plain keys produced for a service (health checks, audits with no facility) are not facility ids.
    // The names live in one fixture so the .NET and Java readers stay the same list.
    private static final Set<String> SERVICE_NAMES = loadServiceNames();

    private KafkaKeyLegacy() {
    }

    public static String tryReadFacility(String key) {
        Opened opened = open(key);
        if (opened == null) {
            return null;
        }
        if (opened.object != null) {
            return nonBlank(textProperty(opened.object, "facilityId"));
        }
        if (SERVICE_NAMES.contains(opened.plain)) {
            return null;
        }
        return opened.plain;
    }

    public static String tryReadPatient(String key) {
        Opened opened = open(key);
        if (opened == null || opened.object == null) {
            return null;
        }
        return nonBlank(textProperty(opened.object, "patientId"));
    }

    public static UUID tryReadReportScheduleId(String key) {
        Opened opened = open(key);
        if (opened == null || opened.object == null) {
            return null;
        }
        String value = nonBlank(textProperty(opened.object, "reportScheduleId"));
        if (value == null) {
            return null;
        }
        String candidate = value.trim();
        if (candidate.startsWith("{") && candidate.endsWith("}") && candidate.length() > 2) {
            candidate = candidate.substring(1, candidate.length() - 1);
        }
        try {
            UUID id = UUID.fromString(candidate);
            return NIL_UUID.equals(id) ? null : id;
        } catch (IllegalArgumentException ex) {
            return null;
        }
    }

    private static Set<String> loadServiceNames() {
        try (InputStream in = KafkaKeyLegacy.class.getResourceAsStream("/kafka-service-names.json")) {
            if (in == null) {
                throw new IllegalStateException("Service name list is missing.");
            }
            JsonNode node = MAPPER.readTree(in);
            if (node == null || !node.isArray() || node.isEmpty()) {
                throw new IllegalStateException("Service name list is empty.");
            }
            Set<String> names = new HashSet<>();
            for (JsonNode item : node) {
                if (!item.isTextual() || item.asText().isEmpty() || !names.add(item.asText())) {
                    throw new IllegalStateException("Service name list contains a blank or duplicate name.");
                }
            }
            return Set.copyOf(names);
        } catch (IOException ex) {
            throw new IllegalStateException("Service name list could not be read.", ex);
        }
    }

    private static String nonBlank(String value) {
        return value == null || value.isBlank() ? null : value;
    }

    private static Opened open(String key) {
        if (key == null || key.isBlank()) {
            return null;
        }
        String trimmed = key.strip();
        if (trimmed.startsWith("{")) {
            try {
                JsonNode node = MAPPER.readTree(trimmed);
                if (node != null && node.isObject()) {
                    return new Opened(node, null);
                }
            } catch (JsonProcessingException ex) {
                return null;
            }
            return null;
        }
        // A non-JSON key that contains ':' is not a plain facility id.
        if (trimmed.indexOf(':') >= 0) {
            return null;
        }
        return new Opened(null, trimmed);
    }

    private static String textProperty(JsonNode object, String name) {
        Iterator<Map.Entry<String, JsonNode>> fields = object.fields();
        while (fields.hasNext()) {
            Map.Entry<String, JsonNode> field = fields.next();
            if (field.getKey().equalsIgnoreCase(name) && field.getValue().isTextual()) {
                return field.getValue().asText();
            }
        }
        return null;
    }

    private static final class Opened {
        private final JsonNode object;
        private final String plain;

        private Opened(JsonNode object, String plain) {
            this.object = object;
            this.plain = plain;
        }
    }
}
