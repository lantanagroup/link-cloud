package com.lantanagroup.link.validation.services;

import com.azure.core.util.BinaryData;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.lantanagroup.link.validation.models.FindingDto;
import com.lantanagroup.link.validation.models.LegacyShadowResultDto;
import com.lantanagroup.link.validation.models.RubricResultDto;
import com.lantanagroup.link.validation.records.ShadowFindingDto;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.nio.charset.StandardCharsets;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.lenient;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

/**
 * Covers {@link ShadowCsvReportService#generateDailyReport} and {@link
 * ShadowCsvReportService#downloadDailyReport} against a mocked {@link BlobStorageService} -- no real Azure
 * client is built or called. The uploaded bytes are read back with a small RFC 4180 parser to confirm the
 * row content and that quoting/escaping round-trips without data loss.
 */
@ExtendWith(MockitoExtension.class)
class ShadowCsvReportServiceTest {

    private static final String DAILY_BLOB_PATH_TEMPLATE = "shadow-reports/shadow-comparison-daily-report.csv";
    private static final String DAILY_BLOB_PATH = "shadow-reports/shadow-comparison-daily-report-2026-08-21.csv";

    @Mock
    private BlobStorageService blobStorageService;

    @Mock
    private RubricResultQueryService rubricResultQueryService;

    @Mock
    private LegacyShadowResultQueryService legacyShadowResultQueryService;

    private final ObjectMapper objectMapper = new ObjectMapper();

    private ShadowCsvReportService service() {
        lenient().when(rubricResultQueryService.findByRequestId(any())).thenReturn(Optional.empty());
        lenient().when(legacyShadowResultQueryService.findByRequestId(any())).thenReturn(Optional.empty());
        return new ShadowCsvReportService(
                Optional.of(blobStorageService), rubricResultQueryService, legacyShadowResultQueryService,
                objectMapper, DAILY_BLOB_PATH_TEMPLATE);
    }

    private static ShadowComparisonResult comparisonResult(boolean matched, String addedJson) {
        return comparisonResult(matched, addedJson, null);
    }

    private static ShadowComparisonResult comparisonResult(boolean matched, String addedJson, UUID requestId) {
        return ShadowComparisonResult.builder()
                .id(UUID.randomUUID())
                .requestId(requestId)
                .correlationId("corr-1")
                .facilityId("facility-1")
                .patientId("patient-1")
                .reportId("report-1")
                .rubricId("rubric-1")
                .ranNewEngine(true)
                .matched(matched)
                .addedCount(matched ? 0 : 1)
                .missingCount(0)
                .severityChangedCount(0)
                .matchedFindingCount(matched ? 1 : 0)
                .addedJson(addedJson)
                .comparedAt(OffsetDateTime.parse("2026-08-21T12:00:00Z"))
                .build();
    }

    private List<List<String>> capturedDailyRows() {
        ArgumentCaptor<BinaryData> captor = ArgumentCaptor.forClass(BinaryData.class);
        verify(blobStorageService).upload(eq(DAILY_BLOB_PATH), captor.capture());
        String csv = new String(captor.getValue().toBytes(), StandardCharsets.UTF_8);
        return parseCsv(csv);
    }

    @Test
    void generateDailyReport_writesHeaderAndOneRowPerResult() {
        ShadowCsvReportService service = service();
        ShadowComparisonResult matched = comparisonResult(true, null);
        ShadowComparisonResult mismatched = comparisonResult(false, "[{\"severity\":\"WARNING\"}]");

        service.generateDailyReport(LocalDate.parse("2026-08-21"), List.of(matched, mismatched));

        List<List<String>> rows = capturedDailyRows();
        List<String> header = rows.get(0);
        assertEquals("Correlation ID", header.get(1));
        assertEquals("Facility ID", header.get(2));
        assertEquals("Is Matched", header.get(9));
        assertEquals("Compared At", header.get(17));

        assertEquals(3, rows.size());
        List<String> matchedRow = rows.get(1);
        assertEquals("true", matchedRow.get(9));
        assertEquals("", matchedRow.get(14));

        List<String> mismatchedRow = rows.get(2);
        assertEquals("false", mismatchedRow.get(9));
        assertTrue(mismatchedRow.get(14).contains("WARNING"));
    }

    @Test
    void generateDailyReport_preservesLargeJsonContent_withoutTruncation() {
        ShadowCsvReportService service = service();
        String largeJson = "[" + "{\"finding\":\"x".repeat(5_000) + "\"}]";
        ShadowComparisonResult result = comparisonResult(false, largeJson);

        service.generateDailyReport(LocalDate.parse("2026-08-21"), List.of(result));

        List<String> row = capturedDailyRows().get(1);
        assertEquals(largeJson.length(), row.get(14).length());
        assertEquals(largeJson, row.get(14));
    }

    @Test
    void generateDailyReport_escapesJsonContainingCommasQuotesBracketsAndNewlines() {
        ShadowCsvReportService service = service();
        String trickyJson = "[{\"message\":\"has, a comma\",\"quote\":\"she said \\\"hi\\\"\","
                + "\"nested\":{\"a\":[1,2,3]},\"multiline\":\"line1\nline2\"}]";
        ShadowComparisonResult result = comparisonResult(false, trickyJson);

        service.generateDailyReport(LocalDate.parse("2026-08-21"), List.of(result));

        List<String> row = capturedDailyRows().get(1);
        assertEquals(trickyJson, row.get(14));
    }

    @Test
    void generateDailyReport_includesRubricAndLegacyFindingsColumns_whenBothAreJoinable() {
        UUID requestId = UUID.randomUUID();
        RubricResultDto rubricResult = RubricResultDto.builder()
                .findings(List.of(FindingDto.builder().checkId("check-1").message("rubric finding").build()))
                .build();
        LegacyShadowResultDto legacyResult = LegacyShadowResultDto.builder()
                .findings(List.of(ShadowFindingDto.builder()
                        .severity(OperationOutcome.IssueSeverity.WARNING)
                        .message("legacy finding")
                        .build()))
                .build();
        when(rubricResultQueryService.findByRequestId(requestId)).thenReturn(Optional.of(rubricResult));
        when(legacyShadowResultQueryService.findByRequestId(requestId)).thenReturn(Optional.of(legacyResult));
        ShadowCsvReportService service = service();

        service.generateDailyReport(
                LocalDate.parse("2026-08-21"), List.of(comparisonResult(true, null, requestId)));

        List<List<String>> rows = capturedDailyRows();
        assertEquals("Rubric Findings", rows.get(0).get(7));
        assertEquals("Legacy Findings", rows.get(0).get(8));

        List<String> row = rows.get(1);
        assertTrue(row.get(7).contains("rubric finding"));
        assertTrue(row.get(8).contains("legacy finding"));
    }

    @Test
    void generateDailyReport_leavesRubricAndLegacyColumnsBlank_whenComparisonHasNoRequestId() {
        ShadowCsvReportService service = service();

        service.generateDailyReport(
                LocalDate.parse("2026-08-21"), List.of(comparisonResult(true, null, null)));

        List<String> row = capturedDailyRows().get(1);
        assertEquals("", row.get(7));
        assertEquals("", row.get(8));
        verifyNoInteractions(rubricResultQueryService, legacyShadowResultQueryService);
    }

    @Test
    void generateDailyReport_doesNothing_whenBlobStorageNotConfigured() {
        ShadowCsvReportService service = new ShadowCsvReportService(
                Optional.empty(), rubricResultQueryService, legacyShadowResultQueryService, objectMapper,
                DAILY_BLOB_PATH_TEMPLATE);

        service.generateDailyReport(LocalDate.parse("2026-08-21"), List.of(comparisonResult(true, null)));

        verifyNoInteractions(blobStorageService);
    }

    @Test
    void generateDailyReport_writesToADateSuffixedDailyCsvBlobPath() {
        ShadowCsvReportService service = service();

        service.generateDailyReport(LocalDate.parse("2026-08-22"), List.of());

        verify(blobStorageService).upload(
                eq("shadow-reports/shadow-comparison-daily-report-2026-08-22.csv"), any());
    }

    @Test
    void downloadDailyReport_returnsBlobBytes_whenReportExists() {
        byte[] bytes = "csv-bytes".getBytes();
        when(blobStorageService.downloadIfExists(DAILY_BLOB_PATH)).thenReturn(BinaryData.fromBytes(bytes));
        ShadowCsvReportService service = service();

        byte[] result = service.downloadDailyReport(LocalDate.parse("2026-08-21"));

        assertEquals("csv-bytes", new String(result));
    }

    @Test
    void downloadDailyReport_returnsNull_whenNoReportForThatDate() {
        when(blobStorageService.downloadIfExists(DAILY_BLOB_PATH)).thenReturn(null);
        ShadowCsvReportService service = service();

        assertNull(service.downloadDailyReport(LocalDate.parse("2026-08-21")));
    }

    @Test
    void downloadDailyReport_returnsNull_whenBlobStorageNotConfigured() {
        ShadowCsvReportService service = new ShadowCsvReportService(
                Optional.empty(), rubricResultQueryService, legacyShadowResultQueryService, objectMapper,
                DAILY_BLOB_PATH_TEMPLATE);

        assertNull(service.downloadDailyReport(LocalDate.parse("2026-08-21")));
        verifyNoInteractions(blobStorageService);
    }

    /** Minimal RFC 4180 parser -- handles quoted fields with embedded commas, quotes, and newlines. */
    private static List<List<String>> parseCsv(String csv) {
        List<List<String>> rows = new ArrayList<>();
        List<String> row = new ArrayList<>();
        StringBuilder field = new StringBuilder();
        boolean inQuotes = false;
        int i = 0;
        while (i < csv.length()) {
            char c = csv.charAt(i);
            if (inQuotes) {
                if (c == '"') {
                    if (i + 1 < csv.length() && csv.charAt(i + 1) == '"') {
                        field.append('"');
                        i++;
                    } else {
                        inQuotes = false;
                    }
                } else {
                    field.append(c);
                }
            } else if (c == '"') {
                inQuotes = true;
            } else if (c == ',') {
                row.add(field.toString());
                field.setLength(0);
            } else if (c == '\r') {
                // skip -- paired with the following \n
            } else if (c == '\n') {
                row.add(field.toString());
                field.setLength(0);
                rows.add(row);
                row = new ArrayList<>();
            } else {
                field.append(c);
            }
            i++;
        }
        if (field.length() > 0 || !row.isEmpty()) {
            row.add(field.toString());
            rows.add(row);
        }
        return rows;
    }
}
