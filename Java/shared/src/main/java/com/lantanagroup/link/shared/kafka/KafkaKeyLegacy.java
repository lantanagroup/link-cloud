package com.lantanagroup.link.shared.kafka;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.util.Iterator;
import java.util.Map;
import java.util.UUID;

// TODO: remove legacy key fallback after one release once producers write ids on the value.
public final class KafkaKeyLegacy {

    private static final ObjectMapper MAPPER = new ObjectMapper();
    private static final UUID NIL_UUID = new UUID(0L, 0L);

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
