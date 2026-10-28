package com.lantanagroup.link.shared.kafka.records.reportstatus;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.MapperFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.lantanagroup.link.shared.kafka.Topics;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.http.converter.json.Jackson2ObjectMapperBuilder;

import java.io.IOException;
import java.io.InputStream;
import java.time.OffsetDateTime;

import static org.junit.jupiter.api.Assertions.*;

/** Shares fixtures with ReportStatusUpdatedContractTests in .NET ServiceTests. */
class ReportStatusUpdatedContractTest {
    private final ObjectMapper mapper = Jackson2ObjectMapperBuilder.json()
            .featuresToEnable(MapperFeature.ACCEPT_CASE_INSENSITIVE_PROPERTIES,
                    MapperFeature.ACCEPT_CASE_INSENSITIVE_ENUMS)
            .featuresToDisable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS)
            .build();

    @ParameterizedTest
    @ValueSource(strings = {"validation", "not-reportable", "failure", "minimal"})
    void deserializeAndRepublish_preservesContractAndOccurrenceTime(String fixture) throws IOException {
        JsonNode expected = readFixture(fixture);
        ReportStatusUpdated value = mapper.treeToValue(expected, ReportStatusUpdated.class);

        assertEquals("0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803", value.getReportScheduleId());
        assertEquals("patient-123", value.getPatientId());
        assertEquals("684e43c1-415b-4d85-9a55-cc9905ddcb63", value.getCorrelationId());
        assertEquals(OffsetDateTime.parse("2026-10-28T13:30:00Z"), value.getTimestamp());
        assertEquals(expected.get("isSuccess").booleanValue(), value.isSuccess());
        String republished = mapper.writeValueAsString(value);
        assertEquals(expected, mapper.readTree(republished));
        assertEquals(value.getTimestamp(), mapper.readValue(republished, ReportStatusUpdated.class).getTimestamp());

        switch (fixture) {
            case "validation" -> {
                assertEquals(PatientReportingStatus.VALIDATION, value.getStatus());
                assertEquals(3_000_000_000L, value.getProcessingTime());
                assertNotNull(value.getValidationResult());
                assertFalse(value.getValidationResult().isPassed());
                assertEquals(4_000_000_000L, value.getValidationResult().getMessageCount());
            }
            case "not-reportable" -> {
                assertEquals(Boolean.FALSE, value.getReportable());
                assertEquals(0L, value.getProcessingTime());
                assertNull(value.getValidationResult());
            }
            case "failure" -> {
                assertEquals("Acquisition failed for log 123.", value.getErrorMessage());
                assertNull(value.getProcessingTime());
            }
            default -> {
                assertNull(value.getReportable());
                assertNull(value.getValidationResult());
                assertNull(value.getErrorMessage());
                assertNull(value.getProcessingTime());
            }
        }
    }

    @Test
    void serialize_producedValidationEvent_matchesSharedContract() throws IOException {
        ReportStatusUpdated value = ReportStatusUpdated.builder()
                .reportScheduleId("0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803")
                .patientId("patient-123")
                .status(PatientReportingStatus.VALIDATION)
                .success(true)
                .processingTime(3_000_000_000L)
                .correlationId("684e43c1-415b-4d85-9a55-cc9905ddcb63")
                .validationResult(ValidationResult.builder().passed(false).messageCount(4_000_000_000L).build())
                .timestamp(OffsetDateTime.parse("2026-10-28T13:30:00Z"))
                .build();
        assertEquals(readFixture("validation"), mapper.readTree(mapper.writeValueAsString(value)));
    }

    @Test
    void statusEnums_matchSharedNamesAndSerializeAsStrings() throws IOException {
        JsonNode statuses = readFixture("statuses");
        assertEquals(PatientReportingStatus.values().length, statuses.get("patientReportingStatuses").size());
        for (int i = 0; i < PatientReportingStatus.values().length; i++) {
            PatientReportingStatus status = PatientReportingStatus.values()[i];
            String wireName = statuses.get("patientReportingStatuses").get(i).textValue();
            assertEquals('"' + wireName + '"', mapper.writeValueAsString(status));
            assertEquals(status, mapper.readValue('"' + wireName + '"', PatientReportingStatus.class));
        }
        assertEquals(ReportStatus.values().length, statuses.get("reportStatuses").size());
        for (int i = 0; i < ReportStatus.values().length; i++) {
            ReportStatus status = ReportStatus.values()[i];
            String wireName = statuses.get("reportStatuses").get(i).textValue();
            assertEquals('"' + wireName + '"', mapper.writeValueAsString(status));
            assertEquals(status, mapper.readValue('"' + wireName + '"', ReportStatus.class));
        }
    }

    @Test
    void topics_haveRetryAndErrorCompanions() {
        assertEquals("ReportStatusUpdated", Topics.REPORT_STATUS_UPDATED);
        assertEquals("ReportStatusUpdated-Retry", Topics.REPORT_STATUS_UPDATED_RETRY);
        assertEquals("ReportStatusUpdated-Error", Topics.REPORT_STATUS_UPDATED_ERROR);
    }

    private JsonNode readFixture(String name) throws IOException {
        try (InputStream stream = getClass().getResourceAsStream("/report-status/" + name + ".json")) {
            assertNotNull(stream, "Missing fixture " + name);
            return mapper.readTree(stream);
        }
    }
}
