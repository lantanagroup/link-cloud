package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.lantanagroup.link.validation.repositories.ShadowComparisonResultRepository;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.List;

/**
 * Once daily, consolidates the previous UTC day's {@code shadow_comparison_result} rows into an Excel
 * workbook and uploads it via {@link ShadowExcelReportService#generateDailyReport}. Safe to re-run --
 * it always rebuilds the full window and overwrites the same blob path.
 */
@Component
@Slf4j
public class ShadowComparisonDailyReportJob {

    private final ShadowComparisonResultRepository shadowComparisonResultRepository;
    private final ShadowExcelReportService shadowExcelReportService;

    public ShadowComparisonDailyReportJob(
            ShadowComparisonResultRepository shadowComparisonResultRepository,
            ShadowExcelReportService shadowExcelReportService) {
        this.shadowComparisonResultRepository = shadowComparisonResultRepository;
        this.shadowExcelReportService = shadowExcelReportService;
    }

    @Scheduled(cron = "${vaas.bridge.shadow-report.daily-cron:0 15 0 * * *}", zone = "UTC")
    public void run() {
        OffsetDateTime endOfWindow = OffsetDateTime.now(ZoneOffset.UTC).toLocalDate().atStartOfDay(ZoneOffset.UTC).toOffsetDateTime();
        OffsetDateTime startOfWindow = endOfWindow.minusDays(1);
        LocalDate reportDate = startOfWindow.toLocalDate();

        try {
            List<ShadowComparisonResult> results =
                    shadowComparisonResultRepository.findByComparedAtBetween(startOfWindow, endOfWindow);
            log.info("Generating daily shadow comparison report for {} with {} row(s)", reportDate, results.size());
            shadowExcelReportService.generateDailyReport(reportDate, results);
        } catch (Exception e) {
            log.warn("Failed to generate the daily shadow comparison report for {}", reportDate, e);
        }
    }
}
