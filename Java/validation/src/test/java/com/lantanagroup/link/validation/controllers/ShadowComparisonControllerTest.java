package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.models.LegacyShadowResultDto;
import com.lantanagroup.link.validation.models.RubricResultDto;
import com.lantanagroup.link.validation.models.ShadowComparisonResultDto;
import com.lantanagroup.link.validation.services.LegacyShadowResultQueryService;
import com.lantanagroup.link.validation.services.RubricResultQueryService;
import com.lantanagroup.link.validation.services.ShadowComparisonQueryService;
import com.lantanagroup.link.validation.services.ShadowExcelReportService;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.http.HttpHeaders;
import org.springframework.test.web.servlet.MockMvc;

import java.time.LocalDate;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(ShadowComparisonController.class)
@AutoConfigureMockMvc(addFilters = false)
class ShadowComparisonControllerTest {

    private static final String BASE = "/api/validation/shadow/comparisons";

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private ShadowComparisonQueryService shadowComparisonQueryService;

    @MockBean
    private RubricResultQueryService rubricResultQueryService;

    @MockBean
    private LegacyShadowResultQueryService legacyShadowResultQueryService;

    @MockBean
    private ShadowExcelReportService shadowExcelReportService;

    @Test
    @DisplayName("GET a known request id returns 200 with the mapped comparison results")
    void returnsPersistedComparisons() throws Exception {
        UUID requestId = UUID.randomUUID();
        ShadowComparisonResultDto dto = ShadowComparisonResultDto.builder()
                .requestId(requestId)
                .reportId("report-1")
                .matched(false)
                .addedCount(1)
                .build();
        when(shadowComparisonQueryService.findByRequestId(requestId)).thenReturn(List.of(dto));
        when(rubricResultQueryService.findByRequestId(requestId)).thenReturn(Optional.empty());
        when(legacyShadowResultQueryService.findByRequestId(requestId)).thenReturn(Optional.empty());

        mockMvc.perform(get(BASE + "/" + requestId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.comparisons[0].reportId").value("report-1"))
                .andExpect(jsonPath("$.comparisons[0].matched").value(false))
                .andExpect(jsonPath("$.comparisons[0].addedCount").value(1))
                .andExpect(jsonPath("$.rubricResult").doesNotExist())
                .andExpect(jsonPath("$.legacyResult").doesNotExist());
    }

    @Test
    @DisplayName("GET a known request id also includes the rubric and legacy engine results it was diffed from")
    void returnsRubricAndLegacyResultsAlongsideComparisons() throws Exception {
        UUID requestId = UUID.randomUUID();
        ShadowComparisonResultDto comparison = ShadowComparisonResultDto.builder()
                .requestId(requestId)
                .reportId("report-1")
                .matched(false)
                .build();
        RubricResultDto rubricResult = RubricResultDto.builder()
                .requestId(requestId)
                .rubricId("measure-report-submission-v1")
                .build();
        LegacyShadowResultDto legacyResult = LegacyShadowResultDto.builder()
                .requestId(requestId)
                .reportId("report-1")
                .errorCount(2)
                .build();
        when(shadowComparisonQueryService.findByRequestId(requestId)).thenReturn(List.of(comparison));
        when(rubricResultQueryService.findByRequestId(requestId)).thenReturn(Optional.of(rubricResult));
        when(legacyShadowResultQueryService.findByRequestId(requestId)).thenReturn(Optional.of(legacyResult));

        mockMvc.perform(get(BASE + "/" + requestId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.rubricResult.rubricId").value("measure-report-submission-v1"))
                .andExpect(jsonPath("$.legacyResult.errorCount").value(2))
                .andExpect(jsonPath("$.comparisons[0].reportId").value("report-1"));
    }

    @Test
    @DisplayName("GET an unknown request id returns 404")
    void returnsNotFoundForUnknownId() throws Exception {
        UUID requestId = UUID.randomUUID();
        when(shadowComparisonQueryService.findByRequestId(requestId)).thenReturn(List.of());

        mockMvc.perform(get(BASE + "/" + requestId))
                .andExpect(status().isNotFound());
    }

    @Test
    @DisplayName("GET the daily report for a date that has one returns 200 with the xlsx bytes")
    void returnsDailyReportBytes() throws Exception {
        byte[] bytes = "workbook-bytes".getBytes();
        when(shadowExcelReportService.downloadDailyReport(LocalDate.parse("2026-08-21"))).thenReturn(bytes);

        mockMvc.perform(get(BASE + "/daily-report").param("date", "2026-08-21"))
                .andExpect(status().isOk())
                .andExpect(header().string(HttpHeaders.CONTENT_DISPOSITION,
                        "attachment; filename=\"shadow-comparison-daily-report-2026-08-21.xlsx\""))
                .andExpect(content().bytes(bytes));
    }

    @Test
    @DisplayName("GET the daily report with no date defaults to yesterday (UTC)")
    void defaultsToYesterdayWhenDateOmitted() throws Exception {
        LocalDate yesterday = LocalDate.now(java.time.ZoneOffset.UTC).minusDays(1);
        when(shadowExcelReportService.downloadDailyReport(yesterday)).thenReturn("bytes".getBytes());

        mockMvc.perform(get(BASE + "/daily-report"))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("GET the daily report for a date with no generated report returns 404")
    void returnsNotFoundWhenNoDailyReportForDate() throws Exception {
        when(shadowExcelReportService.downloadDailyReport(LocalDate.parse("2026-08-21"))).thenReturn(null);

        mockMvc.perform(get(BASE + "/daily-report").param("date", "2026-08-21"))
                .andExpect(status().isNotFound());
    }
}
