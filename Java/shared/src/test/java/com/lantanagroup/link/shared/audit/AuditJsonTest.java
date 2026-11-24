package com.lantanagroup.link.shared.audit;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import java.time.Instant;
import java.util.ArrayList;
import java.util.EnumMap;
import java.util.List;
import java.util.Map;

public class AuditJsonTest {
    private static final List<String> WIRE_NAMES = List.of(
            "FacilityId", "ServiceName", "CorrelationId", "EventDate", "UserId", "User", "Action",
            "Resource", "ResourceId", "PatientId", "PropertyChanges", "Notes");

    @Test
    public void wireMapper_writesEveryNull() {
        JsonNode tree = AuditJson.wireMapper().valueToTree(minimalEvent(Instant.parse("2026-11-23T15:04:05.123Z")));

        Assertions.assertEquals(WIRE_NAMES, fieldNames(tree));
        for (String name : WIRE_NAMES) {
            if (!name.equals("Action") && !name.equals("EventDate")) {
                Assertions.assertTrue(tree.get(name).isNull(), name + " should be written as null");
            }
        }
    }

    @ParameterizedTest
    @ValueSource(strings = {"2026-11-23T15:04:05.123Z", "2026-11-23T15:04:05.120Z", "2026-11-23T15:04:05.000Z"})
    public void wireMapper_eventDate_isUtcTextEndingInZ(String instant) {
        Instant eventDate = Instant.parse(instant);

        JsonNode value = AuditJson.wireMapper().valueToTree(minimalEvent(eventDate)).get("EventDate");

        Assertions.assertTrue(value.isTextual(), "EventDate should be text, was " + value.getNodeType());
        Assertions.assertTrue(value.asText().endsWith("Z"), value.asText());
        Assertions.assertEquals(eventDate, Instant.parse(value.asText()));
    }

    // The exact texts AuditEventMessageContractTests.Consume_JavaEventDate_ReadsTrailingZeroAndWholeSecondForms
    // reads on the .NET side; Java must write each of them as given.
    @ParameterizedTest
    @ValueSource(strings = {"2026-11-23T15:04:05.120Z", "2026-11-23T15:04:05.100Z", "2026-11-23T15:04:05Z"})
    public void wireMapper_eventDate_isTheTextDotNetReads(String literal) {
        JsonNode value = AuditJson.wireMapper().valueToTree(minimalEvent(Instant.parse(literal))).get("EventDate");

        Assertions.assertEquals(literal, value.asText());
    }

    @Test
    public void wireMapper_action_usesDotNetMemberNames() {
        Map<AuditAction, String> expected = new EnumMap<>(AuditAction.class);
        expected.put(AuditAction.CREATE, "Create");
        expected.put(AuditAction.UPDATE, "Update");
        expected.put(AuditAction.DELETE, "Delete");
        expected.put(AuditAction.QUERY, "Query");
        expected.put(AuditAction.SUBMIT, "Submit");
        expected.put(AuditAction.RESTORE, "Restore");
        expected.put(AuditAction.SOFT_DELETE, "SoftDelete");
        Assertions.assertEquals(AuditAction.values().length, expected.size());

        ObjectMapper mapper = AuditJson.wireMapper();
        for (Map.Entry<AuditAction, String> entry : expected.entrySet()) {
            AuditEventMessage event = new AuditEventMessage(null, null, null, null, null, null, entry.getKey(),
                    null, null, null, null, null);
            Assertions.assertEquals(entry.getValue(), mapper.valueToTree(event).get("Action").asText());
        }
    }

    @Test
    public void wireMapper_isUnaffectedByHostNamingStrategy() {
        ObjectMapper mapper = AuditJson.wireMapper();

        Assertions.assertNull(mapper.getPropertyNamingStrategy());
        JsonNode tree = mapper.valueToTree(new AuditEventMessage(null, null, null, null, null, null, AuditAction.UPDATE,
                null, null, null, List.of(new PropertyChange("Title", "Old", "New")), null));
        Assertions.assertEquals(WIRE_NAMES, fieldNames(tree));
        Assertions.assertEquals(List.of("PropertyName", "InitialPropertyValue", "NewPropertyValue", "Entity", "RowKey"),
                fieldNames(tree.get("PropertyChanges").get(0)));
    }

    @Test
    public void logLine_isOneLineAndEscapesNonAscii() {
        AuditEventMessage event = new AuditEventMessage(null, null, null, null, null, null, AuditAction.UPDATE,
                null, null, null, null, "a\nbé");

        String line = AuditJson.logLine(event);

        Assertions.assertFalse(line.contains("\n"), line);
        Assertions.assertFalse(line.contains("\r"), line);
        Assertions.assertFalse(line.contains("é"), line);
        Assertions.assertTrue(line.contains("\\u00e9") || line.contains("\\u00E9"), line);
        Assertions.assertTrue(line.contains("a\\nb"), line);
    }

    private static AuditEventMessage minimalEvent(Instant eventDate) {
        return new AuditEventMessage(null, null, null, eventDate, null, null, AuditAction.UPDATE,
                null, null, null, null, null);
    }

    private static List<String> fieldNames(JsonNode node) {
        List<String> names = new ArrayList<>();
        node.fieldNames().forEachRemaining(names::add);
        return names;
    }
}
