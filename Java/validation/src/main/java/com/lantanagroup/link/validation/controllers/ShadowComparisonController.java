package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.models.ShadowComparisonDetailDto;
import com.lantanagroup.link.validation.models.ShadowComparisonResultDto;
import com.lantanagroup.link.validation.services.LegacyShadowResultQueryService;
import com.lantanagroup.link.validation.services.RubricResultQueryService;
import com.lantanagroup.link.validation.services.ShadowComparisonQueryService;
import com.lantanagroup.link.validation.services.ShadowExcelReportService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;
import org.springframework.format.annotation.DateTimeFormat;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.time.LocalDate;
import java.time.ZoneOffset;
import java.util.List;
import java.util.UUID;

/**
 * ADR-0003 shadow-run: TEMPORARY -- lets an engineer inspect a shadow comparison's diff (added/missing/
 * severity-changed findings) by the rubric request id it was compared against, alongside the rubric and
 * legacy engine results the diff was computed from, without querying {@code shadow_comparison_result},
 * {@code rubric_result}, or {@code legacy_shadow_result} directly, and download the {@code
 * ShadowComparisonDailyReportJob}'s daily Excel workbook straight from blob storage. Remove this
 * controller, {@link ShadowComparisonQueryService}, {@link LegacyShadowResultQueryService}, {@link
 * ShadowComparisonResultDto} and its sibling DTOs, and the report-download plumbing in {@link
 * ShadowExcelReportService} once the shadow period ends and ADR-0003 cuts over.
 */
@RestController
@RequestMapping("/api/validation/shadow/comparisons")
@RequiredArgsConstructor
@Tag(name = "Shadow Comparisons (temporary)", description = "ADR-0003 shadow-run: inspect parallel-run diffs; removed once shadow ends")
public class ShadowComparisonController {

    private static final MediaType XLSX_MEDIA_TYPE =
            MediaType.parseMediaType("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

    private final ShadowComparisonQueryService shadowComparisonQueryService;
    private final RubricResultQueryService rubricResultQueryService;
    private final LegacyShadowResultQueryService legacyShadowResultQueryService;
    private final ShadowExcelReportService shadowExcelReportService;

    @Operation(summary = "Fetch shadow comparison results for a rubric request id, alongside the rubric and "
            + "legacy engine results they were diffed from (temporary, ADR-0003 shadow-run)")
    @GetMapping()
    public ResponseEntity<ShadowComparisonDetailDto> getByRequestId(@RequestParam("requestId") UUID requestId) {
        List<ShadowComparisonResultDto> comparisons = shadowComparisonQueryService.findByRequestId(requestId);
        if (comparisons.isEmpty()) {
            return ResponseEntity.notFound().build();
        }
        ShadowComparisonDetailDto detail = ShadowComparisonDetailDto.builder()
                .rubricResult(rubricResultQueryService.findByRequestId(requestId).orElse(null))
                .legacyResult(legacyShadowResultQueryService.findByRequestId(requestId).orElse(null))
                .comparisons(comparisons)
                .build();
        return ResponseEntity.ok(detail);
    }

    @Operation(summary = "Downloads the daily shadow comparison Excel report for a date, "
            + "defaulting to yesterday (UTC) -- the window ShadowComparisonDailyReportJob last ran for "
            + "(temporary, ADR-0003 shadow-run)")
    @GetMapping("/daily-report")
    public ResponseEntity<byte[]> getDailyReport(
            @RequestParam(required = false) @DateTimeFormat(iso = DateTimeFormat.ISO.DATE) LocalDate date) {
        LocalDate reportDate = date != null ? date : LocalDate.now(ZoneOffset.UTC).minusDays(1);
        byte[] content = shadowExcelReportService.downloadDailyReport(reportDate);
        if (content == null) {
            return ResponseEntity.notFound().build();
        }
        String filename = "shadow-comparison-daily-report-" + reportDate + ".xlsx";
        return ResponseEntity.ok()
                .contentType(XLSX_MEDIA_TYPE)
                .header(HttpHeaders.CONTENT_DISPOSITION, "attachment; filename=\"" + filename + "\"")
                .body(content);
    }
}
