package com.lantanagroup.link.shared.kafka.records.retention;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.MapperFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;
import org.springframework.http.converter.json.Jackson2ObjectMapperBuilder;

import java.io.IOException;
import java.io.InputStream;
import java.time.OffsetDateTime;
import java.util.List;

/**
 * The fixtures under src/test/resources/retention are shared with the .NET tests (RetentionMessageContractTests
 * in ServiceTests). The completions are what Java produces and Report reads; the requests are what Report
 * produces and Java reads.
 */
public class RetentionMessageContractTest {
    private static final String CLEANUP_ID = "6f1c2a9e-4b7d-4c1e-9a53-2d8e7f0b1c34";
    private static final String FIRST_REPORT_ID = "0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803";
    private static final String SECOND_REPORT_ID = "c3d94a17-5e2b-4f60-8d1a-9b7e2f4c6a05";

    // Matches the services: case-insensitive properties and enums come from their application.yml, and the
    // ObjectMapper bean writes dates as ISO strings.
    private final ObjectMapper mapper = Jackson2ObjectMapperBuilder.json()
            .featuresToEnable(MapperFeature.ACCEPT_CASE_INSENSITIVE_PROPERTIES,
                              MapperFeature.ACCEPT_CASE_INSENSITIVE_ENUMS)
            .featuresToDisable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS)
            .build();

    @Test
    public void serialize_completionWithReports_matchesSharedFixture() throws IOException {
        RetentionCheckComplete completion = RetentionCheckComplete.builder()
                .retentionCleanupId(CLEANUP_ID)
                .serviceName("MeasureEval")
                .completedDateTime(OffsetDateTime.parse("2026-10-01T06:12:30Z"))
                .reports(List.of(
                        RetentionReportResult.builder()
                                .reportScheduleId(FIRST_REPORT_ID)
                                .status(RetentionResultStatus.COMPLETED)
                                .recordsAffected(3_000_000_000L)
                                .completedDateTime(OffsetDateTime.parse("2026-10-01T06:10:05Z"))
                                .build(),
                        RetentionReportResult.builder()
                                .reportScheduleId(SECOND_REPORT_ID)
                                .status(RetentionResultStatus.FAILED)
                                .recordsAffected(5000)
                                .errorMessage("Deleting the report's resources failed: MongoTimeoutException")
                                .completedDateTime(OffsetDateTime.parse("2026-10-01T06:12:30Z"))
                                .build()))
                .build();

        Assertions.assertEquals(readFixture("retention-check-complete-reports.json"), toTree(completion));
    }

    @Test
    public void serialize_completionWithSummary_matchesSharedFixture() throws IOException {
        RetentionCheckComplete completion = RetentionCheckComplete.builder()
                .retentionCleanupId(CLEANUP_ID)
                .serviceName("Census")
                .completedDateTime(OffsetDateTime.parse("2026-10-01T06:01:45Z"))
                .summary(RetentionReportResult.builder()
                        .status(RetentionResultStatus.COMPLETED)
                        .recordsAffected(0)
                        .completedDateTime(OffsetDateTime.parse("2026-10-01T06:01:45Z"))
                        .build())
                .build();

        Assertions.assertEquals(readFixture("retention-check-complete-summary.json"), toTree(completion));
    }

    @Test
    public void deserialize_scheduledRequestFromDotNet_readsEveryField() throws IOException {
        RetentionCheckRequested request = mapper.treeToValue(
                readFixture("retention-check-requested-scheduled.json"),
                RetentionCheckRequested.class);

        Assertions.assertEquals(CLEANUP_ID, request.getRetentionCleanupId());
        Assertions.assertEquals(RetentionInitiatedBy.SCHEDULED, request.getInitiatedBy());
        Assertions.assertEquals(OffsetDateTime.parse("2026-10-01T06:00:00Z"), request.getInitiatedDateTime());
        Assertions.assertEquals(RetentionMode.SCRUB, request.getMode());
        Assertions.assertEquals(OffsetDateTime.parse("2025-10-01T06:00:00Z"), request.getCutoffDateTime());
        Assertions.assertEquals(List.of("MeasureEval", "Validation"), request.getParticipants());
        Assertions.assertEquals(2, request.getReports().size());

        RetentionReport first = request.getReports().get(0);
        Assertions.assertEquals("Hospital1", first.getFacilityId());
        Assertions.assertEquals(FIRST_REPORT_ID, first.getReportScheduleId());
        Assertions.assertEquals(new RetentionWindow(RetentionWindowUnit.MONTHS, 12), first.getRetentionWindow());
        Assertions.assertEquals(OffsetDateTime.parse("2025-10-01T06:00:00Z"), first.getCutoffDateTime());

        RetentionReport second = request.getReports().get(1);
        Assertions.assertEquals("Hospital2", second.getFacilityId());
        Assertions.assertEquals(SECOND_REPORT_ID, second.getReportScheduleId());
        Assertions.assertEquals(new RetentionWindow(RetentionWindowUnit.DAYS, 400), second.getRetentionWindow());
        Assertions.assertEquals(OffsetDateTime.parse("2025-08-27T06:00:00Z"), second.getCutoffDateTime());
    }

    @Test
    public void deserialize_singleReportRequestFromDotNet_leavesOptionalFieldsNull() throws IOException {
        RetentionCheckRequested request = mapper.treeToValue(
                readFixture("retention-check-requested-single-report.json"),
                RetentionCheckRequested.class);

        Assertions.assertEquals(RetentionInitiatedBy.SINGLE_REPORT, request.getInitiatedBy());
        Assertions.assertEquals(RetentionMode.PURGE, request.getMode());
        Assertions.assertNull(request.getParticipants());
        Assertions.assertEquals(1, request.getReports().size());
        Assertions.assertNull(request.getReports().get(0).getRetentionWindow());
        Assertions.assertEquals(OffsetDateTime.parse("2026-10-22T15:30:00Z"),
                                request.getReports().get(0).getCutoffDateTime());
    }

    // Through text rather than valueToTree, which makes every long a LongNode while the parsed fixture holds
    // small numbers as IntNode, and the two never compare equal.
    private JsonNode toTree(Object value) throws IOException {
        return mapper.readTree(mapper.writeValueAsString(value));
    }

    private JsonNode readFixture(String name) throws IOException {
        try (InputStream stream = getClass().getResourceAsStream("/retention/" + name)) {
            Assertions.assertNotNull(stream, "Missing fixture " + name);
            return mapper.readTree(stream);
        }
    }
}
