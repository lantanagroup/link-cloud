package com.lantanagroup.link.validation.services;

import com.azure.core.util.BinaryData;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import lombok.extern.slf4j.Slf4j;
import org.apache.poi.ss.SpreadsheetVersion;
import org.apache.poi.ss.usermodel.Row;
import org.apache.poi.ss.usermodel.Sheet;
import org.apache.poi.ss.usermodel.Workbook;
import org.apache.poi.xssf.usermodel.XSSFWorkbook;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.io.ByteArrayOutputStream;
import java.time.LocalDate;
import java.time.format.DateTimeFormatter;
import java.util.List;
import java.util.Optional;

/**
 * Builds the daily Excel workbook of shadow comparisons and uploads it to blob storage. The report date
 * is inserted into the templated blob path before the extension, e.g.
 * {@code shadow-comparison-daily-report.xlsx} -> {@code shadow-comparison-daily-report-2026-08-24.xlsx}.
 */
@Service
@Slf4j
public class ShadowExcelReportService {

    private static final String SHEET_NAME = "Shadow Comparisons";
    private static final String[] DAILY_HEADERS = {
            "Request ID", "Correlation ID", "Facility ID", "Patient ID", "Report ID", "Rubric ID",
            "Ran New Engine", "Is Matched", "Added Count", "Missing Count", "Severity Changed Count",
            "Matched Finding Count", "Added Findings", "Missing Findings", "Severity Changed", "Compared At"
    };
    private static final int MAX_CELL_TEXT_LENGTH = SpreadsheetVersion.EXCEL2007.getMaxTextLength();
    private static final String TRUNCATION_SUFFIX = "...[truncated]";

    private final BlobStorageService blobStorageService;
    private final String dailyBlobPathTemplate;

    public ShadowExcelReportService(
            Optional<BlobStorageService> blobStorageService,
            @Value("${vaas.bridge.shadow-report.daily-blob-path:shadow-reports/shadow-comparison-daily-report.xlsx}") String dailyBlobPathTemplate) {
        this.blobStorageService = blobStorageService.orElse(null);
        this.dailyBlobPathTemplate = dailyBlobPathTemplate;
    }

    /**
     * Builds one workbook -- one row per {@link ShadowComparisonResult} -- for a single day and uploads
     * it. Always starts fresh since {@code results} is already the complete row set for {@code reportDate}.
     */
    public void generateDailyReport(LocalDate reportDate, List<ShadowComparisonResult> results) {
        if (blobStorageService == null) {
            return;
        }
        String blobPath = resolveBlobPath(dailyBlobPathTemplate, reportDate);
        try (Workbook workbook = new XSSFWorkbook()) {
            Sheet sheet = workbook.createSheet(SHEET_NAME);
            writeDailyHeader(sheet);
            int rowIndex = 1;
            for (ShadowComparisonResult result : results) {
                writeDailyRow(sheet, rowIndex++, result);
            }
            blobStorageService.upload(blobPath, BinaryData.fromBytes(toBytes(workbook)));
        } catch (Exception e) {
            log.warn("Failed to generate the daily shadow comparison report for {}", reportDate, e);
        }
    }

    /**
     * Downloads the workbook {@link #generateDailyReport} published for {@code reportDate}, or
     * {@code null} if blob storage isn't configured or nothing's been generated for that date yet.
     */
    public byte[] downloadDailyReport(LocalDate reportDate) {
        if (blobStorageService == null) {
            return null;
        }
        String blobPath = resolveBlobPath(dailyBlobPathTemplate, reportDate);
        BinaryData data = blobStorageService.downloadIfExists(blobPath);
        return data == null ? null : data.toBytes();
    }

    /** Inserts {@code date} before the given template's extension so each day gets its own blob. */
    private static String resolveBlobPath(String template, LocalDate date) {
        String datePart = date.format(DateTimeFormatter.ISO_LOCAL_DATE);
        int dot = template.lastIndexOf('.');
        return dot < 0
                ? template + "-" + datePart
                : template.substring(0, dot) + "-" + datePart + template.substring(dot);
    }

    private void writeDailyHeader(Sheet sheet) {
        Row header = sheet.createRow(0);
        for (int i = 0; i < DAILY_HEADERS.length; i++) {
            header.createCell(i).setCellValue(DAILY_HEADERS[i]);
        }
    }

    private void writeDailyRow(Sheet sheet, int rowIndex, ShadowComparisonResult result) {
        Row row = sheet.createRow(rowIndex);
        row.createCell(0).setCellValue(result.getId() == null ? "" : result.getRequestId().toString());
        row.createCell(1).setCellValue(nullToEmpty(result.getCorrelationId()));
        row.createCell(2).setCellValue(nullToEmpty(result.getFacilityId()));
        row.createCell(3).setCellValue(nullToEmpty(result.getPatientId()));
        row.createCell(4).setCellValue(nullToEmpty(result.getReportId()));
        row.createCell(5).setCellValue(nullToEmpty(result.getRubricId()));
        row.createCell(6).setCellValue(result.isRanNewEngine());
        row.createCell(7).setCellValue(result.isMatched());
        row.createCell(8).setCellValue(result.getAddedCount());
        row.createCell(9).setCellValue(result.getMissingCount());
        row.createCell(10).setCellValue(result.getSeverityChangedCount());
        row.createCell(11).setCellValue(result.getMatchedFindingCount());
        row.createCell(12).setCellValue(nullToEmpty(truncateForCell(result.getAddedJson())));
        row.createCell(13).setCellValue(nullToEmpty(truncateForCell(result.getMissingJson())));
        row.createCell(14).setCellValue(nullToEmpty(truncateForCell(result.getSeverityChangedJson())));
        row.createCell(15).setCellValue(result.getComparedAt() == null ? "" : result.getComparedAt().toString());
    }

    private byte[] toBytes(Workbook workbook) throws Exception {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        workbook.write(out);
        return out.toByteArray();
    }

    private String nullToEmpty(String value) {
        return value == null ? "" : value;
    }

    /**
     * Truncates so a long finding list doesn't exceed Excel's per-cell character limit and drop the
     * whole row. The full result set is still available in {@code shadow_comparison_result}'s JSON
     * columns.
     */
    private String truncateForCell(String value) {
        if (value == null || value.length() <= MAX_CELL_TEXT_LENGTH) {
            return value;
        }
        return value.substring(0, MAX_CELL_TEXT_LENGTH - TRUNCATION_SUFFIX.length()) + TRUNCATION_SUFFIX;
    }
}
