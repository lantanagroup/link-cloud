package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import com.lantanagroup.link.validation.repositories.ShadowComparisonResultRepository;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class ShadowComparisonDailyReportJobTest {

    @Mock
    private ShadowComparisonResultRepository shadowComparisonResultRepository;
    @Mock
    private ShadowCsvReportService shadowCsvReportService;

    @Test
    void run_queriesThePreviousUtcDayAndForwardsResultsToTheReportService() {
        ShadowComparisonDailyReportJob job =
                new ShadowComparisonDailyReportJob(shadowComparisonResultRepository, shadowCsvReportService);
        ShadowComparisonResult result = ShadowComparisonResult.builder().build();
        when(shadowComparisonResultRepository.findByComparedAtBetween(any(), any())).thenReturn(List.of(result));

        job.run();

        LocalDate today = OffsetDateTime.now(ZoneOffset.UTC).toLocalDate();
        OffsetDateTime expectedEnd = today.atStartOfDay(ZoneOffset.UTC).toOffsetDateTime();
        OffsetDateTime expectedStart = expectedEnd.minusDays(1);

        ArgumentCaptor<OffsetDateTime> startCaptor = ArgumentCaptor.forClass(OffsetDateTime.class);
        ArgumentCaptor<OffsetDateTime> endCaptor = ArgumentCaptor.forClass(OffsetDateTime.class);
        verify(shadowComparisonResultRepository).findByComparedAtBetween(startCaptor.capture(), endCaptor.capture());
        assertEquals(expectedStart, startCaptor.getValue());
        assertEquals(expectedEnd, endCaptor.getValue());

        verify(shadowCsvReportService).generateDailyReport(expectedStart.toLocalDate(), List.of(result));
    }

    @Test
    void run_swallowsExceptions_soASchedulerFailureNeverPropagates() {
        ShadowComparisonDailyReportJob job =
                new ShadowComparisonDailyReportJob(shadowComparisonResultRepository, shadowCsvReportService);
        when(shadowComparisonResultRepository.findByComparedAtBetween(any(), any()))
                .thenThrow(new RuntimeException("boom"));

        job.run();
    }
}
