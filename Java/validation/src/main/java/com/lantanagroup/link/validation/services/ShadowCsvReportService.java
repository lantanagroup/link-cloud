package com.lantanagroup.link.validation.services;

import com.azure.core.util.BinaryData;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.lantanagroup.link.validation.models.LegacyShadowResultDto;
import com.lantanagroup.link.validation.models.RubricResultDto;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.OutputStreamWriter;
import java.io.Writer;
import java.nio.charset.StandardCharsets;
import java.time.LocalDate;
import java.time.format.DateTimeFormatter;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import java.util.function.Function;

/**
 * Builds the daily CSV of shadow comparisons and uploads it to blob storage. The report date is
 * inserted into the templated blob path before the extension, e.g. {@code
 * shadow-comparison-daily-report.csv} -> {@code shadow-comparison-daily-report-2026-08-24.csv}. Alongside
 * each comparison's diff, every row also carries the rubric and legacy engine results the diff was
 * computed from -- looked up by the comparison's {@code request_id}, which is null (and so are these
 * columns) when the legacy engine ran primary.
 */
@Service
@Slf4j
public class ShadowCsvReportService {

    private static final String[] DAILY_HEADERS = {
            "Request ID", "Correlation ID", "Facility ID", "Patient ID", "Report ID", "Rubric ID",
            "Ran New Engine", "Rubric Findings", "Legacy Findings", "Is Matched", "Added Count", "Missing Count",
            "Severity Changed Count", "Matched Finding Count", "Added Findings", "Missing Findings",
            "Severity Changed", "Compared At"
    };

    private final BlobStorageService blobStorageService;
    private final RubricResultQueryService rubricResultQueryService;
    private final LegacyShadowResultQueryService legacyShadowResultQueryService;
    private final ObjectMapper objectMapper;
    private final String dailyBlobPathTemplate;

    public ShadowCsvReportService(
            Optional<BlobStorageService> blobStorageService,
            RubricResultQueryService rubricResultQueryService,
            LegacyShadowResultQueryService legacyShadowResultQueryService,
            ObjectMapper objectMapper,
            @Value("${vaas.bridge.shadow-report.daily-blob-path:shadow-reports/shadow-comparison-daily-report.csv}") String dailyBlobPathTemplate) {
        this.blobStorageService = blobStorageService.orElse(null);
        this.rubricResultQueryService = rubricResultQueryService;
        this.legacyShadowResultQueryService = legacyShadowResultQueryService;
        this.objectMapper = objectMapper;
        this.dailyBlobPathTemplate = dailyBlobPathTemplate;
    }

    /**
     * Builds one CSV -- one row per {@link ShadowComparisonResult} -- for a single day and uploads it.
     * Always starts fresh since {@code results} is already the complete row set for {@code reportDate}.
     */
    public void generateDailyReport(LocalDate reportDate, List<ShadowComparisonResult> results) {
        if (blobStorageService == null) {
            return;
        }
        String blobPath = resolveBlobPath(dailyBlobPathTemplate, reportDate);
        try {
            blobStorageService.upload(blobPath, BinaryData.fromBytes(buildCsv(results)));
        } catch (Exception e) {
            log.warn("Failed to generate the daily shadow comparison report for {}", reportDate, e);
        }
    }

    /**
     * Downloads the CSV {@link #generateDailyReport} published for {@code reportDate}, or {@code null}
     * if blob storage isn't configured or nothing's been generated for that date yet.
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

    private byte[] buildCsv(List<ShadowComparisonResult> results) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        try (Writer writer = new OutputStreamWriter(out, StandardCharsets.UTF_8)) {
            writeRow(writer, DAILY_HEADERS);
            for (ShadowComparisonResult result : results) {
                writeRow(writer, toRow(result));
            }
        }
        return out.toByteArray();
    }

    private String[] toRow(ShadowComparisonResult result) {
        RubricResultDto rubric = lookUp(rubricResultQueryService::findByRequestId, result.getRequestId());
        LegacyShadowResultDto legacy = lookUp(legacyShadowResultQueryService::findByRequestId, result.getRequestId());

        return new String[]{
                result.getId() == null ? "" : result.getRequestId().toString(),
                nullToEmpty(result.getCorrelationId()),
                nullToEmpty(result.getFacilityId()),
                nullToEmpty(result.getPatientId()),
                nullToEmpty(result.getReportId()),
                nullToEmpty(result.getRubricId()),
                Boolean.toString(result.isRanNewEngine()),
                rubric == null ? "" : writeFindingsJson(rubric.getFindings(), "rubric", result.getReportId()),
                legacy == null ? "" : writeFindingsJson(legacy.getFindings(), "legacy", result.getReportId()),
                Boolean.toString(result.isMatched()),
                Integer.toString(result.getAddedCount()),
                Integer.toString(result.getMissingCount()),
                Integer.toString(result.getSeverityChangedCount()),
                Integer.toString(result.getMatchedFindingCount()),
                nullToEmpty(result.getAddedJson()),
                nullToEmpty(result.getMissingJson()),
                nullToEmpty(result.getSeverityChangedJson()),
                result.getComparedAt() == null ? "" : result.getComparedAt().toString()
        };
    }

    /** {@code request_id} is null when the legacy engine ran primary -- no rubric request to join on. */
    private <T> T lookUp(Function<UUID, Optional<T>> findByRequestId, UUID requestId) {
        return requestId == null ? null : findByRequestId.apply(requestId).orElse(null);
    }

    private String writeFindingsJson(List<?> findings, String label, String reportId) {
        if (findings == null || findings.isEmpty()) {
            return "";
        }
        try {
            return objectMapper.writeValueAsString(findings);
        } catch (Exception e) {
            log.warn("Failed to serialize {} findings for report {}", label, reportId, e);
            return "";
        }
    }

    private void writeRow(Writer writer, String[] values) throws IOException {
        for (int i = 0; i < values.length; i++) {
            if (i > 0) {
                writer.write(',');
            }
            writer.write(escapeCsv(values[i]));
        }
        writer.write("\r\n");
    }

    private String nullToEmpty(String value) {
        return value == null ? "" : value;
    }

    /**
     * Quotes a field per RFC 4180 when it contains a comma, quote, or newline, doubling any embedded
     * quotes. No length limit is applied -- the full JSON content (added/missing/severity-changed
     * findings) is preserved, unlike the previous Excel-based report's per-cell truncation.
     */
    private static String escapeCsv(String value) {
        if (value.isEmpty()) {
            return value;
        }
        boolean needsQuoting = value.indexOf(',') >= 0 || value.indexOf('"') >= 0
                || value.indexOf('\n') >= 0 || value.indexOf('\r') >= 0;
        if (!needsQuoting) {
            return value;
        }
        return "\"" + value.replace("\"", "\"\"") + "\"";
    }
}
