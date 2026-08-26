package com.lantanagroup.link.validation.services;

import com.azure.core.util.BinaryData;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import org.apache.poi.ss.usermodel.Row;
import org.apache.poi.ss.usermodel.Sheet;
import org.apache.poi.xssf.usermodel.XSSFWorkbook;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

/**
 * Covers {@link ShadowExcelReportService#generateDailyReport} and {@link
 * ShadowExcelReportService#downloadDailyReport} against a mocked {@link BlobStorageService} -- no real Azure
 * client is built or called. The uploaded bytes are read back with POI to confirm the row content.
 */
@ExtendWith(MockitoExtension.class)
class ShadowExcelReportServiceTest {

    private static final String DAILY_BLOB_PATH_TEMPLATE = "shadow-reports/shadow-comparison-daily-report.xlsx";
    private static final String DAILY_BLOB_PATH = "shadow-reports/shadow-comparison-daily-report-2026-08-21.xlsx";

    @Mock
    private BlobStorageService blobStorageService;

    private static ShadowComparisonResult comparisonResult(boolean matched, String addedJson) {
        return ShadowComparisonResult.builder()
                .id(UUID.randomUUID())
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

    private XSSFWorkbook capturedDailyWorkbook() throws Exception {
        ArgumentCaptor<BinaryData> captor = ArgumentCaptor.forClass(BinaryData.class);
        verify(blobStorageService).upload(eq(DAILY_BLOB_PATH), captor.capture());
        return new XSSFWorkbook(captor.getValue().toStream());
    }

    @Test
    void generateDailyReport_writesHeaderAndOneRowPerResult() throws Exception {
        ShadowExcelReportService service = new ShadowExcelReportService(
                Optional.of(blobStorageService), DAILY_BLOB_PATH_TEMPLATE);
        ShadowComparisonResult matched = comparisonResult(true, null);
        ShadowComparisonResult mismatched = comparisonResult(false, "[{\"severity\":\"WARNING\"}]");

        service.generateDailyReport(LocalDate.parse("2026-08-21"), List.of(matched, mismatched));

        try (XSSFWorkbook workbook = capturedDailyWorkbook()) {
            Sheet sheet = workbook.getSheetAt(0);
            Row header = sheet.getRow(0);
            assertEquals("Comparison ID", header.getCell(0).getStringCellValue());
            assertEquals("Correlation ID", header.getCell(1).getStringCellValue());
            assertEquals("Facility ID", header.getCell(2).getStringCellValue());
            assertEquals("Is Matched", header.getCell(7).getStringCellValue());
            assertEquals("Compared At", header.getCell(15).getStringCellValue());

            assertEquals(2, sheet.getLastRowNum());
            Row matchedRow = sheet.getRow(1);
            assertEquals(matched.getId().toString(), matchedRow.getCell(0).getStringCellValue());
            assertTrue(matchedRow.getCell(7).getBooleanCellValue());
            assertEquals("", matchedRow.getCell(12).getStringCellValue());

            Row mismatchedRow = sheet.getRow(2);
            assertFalse(mismatchedRow.getCell(7).getBooleanCellValue());
            assertTrue(mismatchedRow.getCell(12).getStringCellValue().contains("WARNING"));
        }
    }

    @Test
    void generateDailyReport_truncatesOversizedJson_insteadOfDroppingTheRow() throws Exception {
        ShadowExcelReportService service = new ShadowExcelReportService(
                Optional.of(blobStorageService), DAILY_BLOB_PATH_TEMPLATE);
        ShadowComparisonResult result = comparisonResult(false, "x".repeat(40_000));

        service.generateDailyReport(LocalDate.parse("2026-08-21"), List.of(result));

        try (XSSFWorkbook workbook = capturedDailyWorkbook()) {
            String addedCell = workbook.getSheetAt(0).getRow(1).getCell(12).getStringCellValue();
            assertEquals(32767, addedCell.length());
            assertTrue(addedCell.endsWith("...[truncated]"));
        }
    }

    @Test
    void generateDailyReport_doesNothing_whenBlobStorageNotConfigured() {
        ShadowExcelReportService service = new ShadowExcelReportService(
                Optional.empty(), DAILY_BLOB_PATH_TEMPLATE);

        service.generateDailyReport(LocalDate.parse("2026-08-21"), List.of(comparisonResult(true, null)));

        verifyNoInteractions(blobStorageService);
    }

    @Test
    void generateDailyReport_writesToADateSuffixedDailyBlobPath() throws Exception {
        ShadowExcelReportService service = new ShadowExcelReportService(
                Optional.of(blobStorageService), DAILY_BLOB_PATH_TEMPLATE);

        service.generateDailyReport(LocalDate.parse("2026-08-22"), List.of());

        verify(blobStorageService).upload(
                eq("shadow-reports/shadow-comparison-daily-report-2026-08-22.xlsx"), any());
    }

    @Test
    void downloadDailyReport_returnsBlobBytes_whenReportExists() {
        byte[] bytes = "workbook-bytes".getBytes();
        when(blobStorageService.downloadIfExists(DAILY_BLOB_PATH)).thenReturn(BinaryData.fromBytes(bytes));
        ShadowExcelReportService service = new ShadowExcelReportService(
                Optional.of(blobStorageService), DAILY_BLOB_PATH_TEMPLATE);

        byte[] result = service.downloadDailyReport(LocalDate.parse("2026-08-21"));

        assertEquals("workbook-bytes", new String(result));
    }

    @Test
    void downloadDailyReport_returnsNull_whenNoReportForThatDate() {
        when(blobStorageService.downloadIfExists(DAILY_BLOB_PATH)).thenReturn(null);
        ShadowExcelReportService service = new ShadowExcelReportService(
                Optional.of(blobStorageService), DAILY_BLOB_PATH_TEMPLATE);

        assertNull(service.downloadDailyReport(LocalDate.parse("2026-08-21")));
    }

    @Test
    void downloadDailyReport_returnsNull_whenBlobStorageNotConfigured() {
        ShadowExcelReportService service = new ShadowExcelReportService(
                Optional.empty(), DAILY_BLOB_PATH_TEMPLATE);

        assertNull(service.downloadDailyReport(LocalDate.parse("2026-08-21")));
        verifyNoInteractions(blobStorageService);
    }
}
