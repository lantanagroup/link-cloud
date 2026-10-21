package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.PatientCategoryCount;
import com.lantanagroup.link.validation.entities.ReportCategoryCount;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.models.CategoryCountModel;
import com.lantanagroup.link.validation.models.ReportCategoryCountsModel;
import com.lantanagroup.link.validation.repositories.PatientCategoryCountRepository;
import com.lantanagroup.link.validation.repositories.ReportCategoryCountRepository;
import com.lantanagroup.link.validation.repositories.ReportCategoryCountStatusRepository;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.stream.Collectors;

import static org.junit.jupiter.api.Assertions.*;

/**
 * Runs the service against H2 with real transactions (the test itself is not transactional), so the status-row
 * lock and the first-insert race behave as they do in the consumer.
 */
@DataJpaTest(properties = {
        "spring.datasource.url=jdbc:h2:mem:categorycountservice;MODE=MSSQLServer;DB_CLOSE_DELAY=-1;LOCK_TIMEOUT=10000",
        "spring.datasource.driver-class-name=org.h2.Driver",
        "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Transactional(propagation = Propagation.NOT_SUPPORTED)
@Import(CategoryCountService.class)
class CategoryCountServiceJpaTest {
    private static final String FACILITY_ID = "facility";
    private static final String REPORT_ID = "report-1";

    @TestConfiguration
    static class Config {
        @Bean
        ObjectMapper objectMapper() {
            return new ObjectMapper();
        }
    }

    @Autowired
    private CategoryCountService categoryCountService;

    @Autowired
    private ReportCategoryCountRepository reportCategoryCountRepository;

    @Autowired
    private PatientCategoryCountRepository patientCategoryCountRepository;

    @Autowired
    private ReportCategoryCountStatusRepository reportCategoryCountStatusRepository;

    @AfterEach
    void cleanUp() {
        patientCategoryCountRepository.deleteAll();
        reportCategoryCountRepository.deleteAll();
        reportCategoryCountStatusRepository.deleteAll();
    }

    @Test
    void recordPatientCounts_sumsPatientsIntoReportTotals() {
        Category a = category("category-a", true, false);
        Category b = category("category-b", true, true);

        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1",
                List.of(result(a), result(a), uncategorized()));
        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-2",
                List.of(result(a), result(b)));

        assertEquals(Map.of("category-a", 3L, "category-b", 1L, "uncategorized", 1L), totals());
        ReportCategoryCount totalB = total("category-b");
        assertEquals("Title of category-b", totalB.getTitle());
        assertTrue(totalB.isAcceptable());
        ReportCategoryCount totalUncategorized = total("uncategorized");
        assertEquals("Uncategorized", totalUncategorized.getTitle());
        assertFalse(totalUncategorized.isAcceptable());
        assertTrue(totalUncategorized.isSubmit());
    }

    @Test
    void recordPatientCounts_samePatientTwice_leavesTotalsUnchanged() {
        Category a = category("category-a", true, false);
        List<Result> results = List.of(result(a), result(a));

        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1", results);
        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1", results);

        assertEquals(Map.of("category-a", 2L), totals());
        assertEquals(1, patientCounts("patient-1").size());
    }

    @Test
    void recordPatientCounts_patientRecordedAgainWithDifferentFindings_replacesItsContribution() {
        Category a = category("category-a", true, false);
        Category b = category("category-b", true, false);
        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1",
                List.of(result(a), result(a), result(b)));
        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-2", List.of(result(a)));

        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1", List.of(result(a)));

        assertEquals(Map.of("category-a", 2L), totals());
        assertEquals(Map.of("category-a", 1L), patientCounts("patient-1"));
    }

    @Test
    void recordPatientCounts_findingWithOnlyNonSubmitCategories_isCountedWithSubmitFalse() {
        Category notSubmitted = category("category-n", false, false);

        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1", List.of(result(notSubmitted)));

        assertEquals(Map.of("category-n", 1L), totals());
        assertFalse(total("category-n").isSubmit());
    }

    @Test
    void recordPatientCounts_findingInSeveralCategories_countsOnceInEachWithItsSubmitFlag() {
        Category submitted = category("category-a", true, false);
        Category notSubmitted = category("category-n", false, true);

        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1",
                List.of(result(submitted, notSubmitted)));

        assertEquals(Map.of("category-a", 1L, "category-n", 1L), totals());
        assertTrue(total("category-a").isSubmit());
        assertFalse(total("category-n").isSubmit());
    }

    @Test
    void recordPatientCounts_categoryIdsDifferingOnlyByCase_areOneCategory() {
        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1",
                List.of(result(category("Category-A", true, false)), result(category("category-a", true, false))));

        assertEquals(1, totals().size());
        assertEquals(2L, totals().values().iterator().next());
    }

    @Test
    void recordPatientCounts_patientWithNoFindings_stillMarksTheReportAsCounted() {
        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1", Collections.emptyList());

        assertTrue(reportCategoryCountStatusRepository.findByFacilityIdAndReportId(FACILITY_ID, REPORT_ID).isPresent());
        assertTrue(totals().isEmpty());
    }

    @Test
    void recordPatientCounts_reportsAreCountedSeparately() {
        Category a = category("category-a", true, false);

        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1", List.of(result(a)));
        categoryCountService.recordPatientCounts(FACILITY_ID, "report-2", "patient-1", List.of(result(a), result(a)));

        assertEquals(Map.of("category-a", 1L), totals());
        assertEquals(2L, reportCategoryCountRepository.findAllByFacilityIdAndReportId(FACILITY_ID, "report-2")
                .get(0)
                .getIssues());
    }

    @Test
    void recordPatientCounts_concurrentPatientsOfOneReport_produceExactTotals() throws Exception {
        Category a = category("category-a", true, false);
        int patients = 16;
        ExecutorService executor = Executors.newFixedThreadPool(8);
        try {
            List<Future<?>> futures = new ArrayList<>();
            for (int i = 0; i < patients; i++) {
                String patientId = "patient-" + i;
                futures.add(executor.submit(() -> categoryCountService.recordPatientCounts(
                        FACILITY_ID,
                        REPORT_ID,
                        patientId,
                        List.of(result(a), uncategorized()))));
            }
            for (Future<?> future : futures) {
                future.get();
            }
        } finally {
            executor.shutdown();
        }

        assertEquals(Map.of("category-a", (long) patients, "uncategorized", (long) patients), totals());
    }

    @Test
    void getReportCounts_reportNeverCounted_isEmpty() {
        assertTrue(categoryCountService.getReportCounts(FACILITY_ID, REPORT_ID, null).isEmpty());
    }

    @Test
    void getReportCounts_returnsTotalsMostIssuesFirst() {
        Category a = category("category-a", true, false);
        Category n = category("category-n", false, true);
        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1",
                List.of(result(a), result(n), result(n), uncategorized(), uncategorized(), uncategorized()));

        ReportCategoryCountsModel counts = categoryCountService.getReportCounts(FACILITY_ID, REPORT_ID, null)
                .orElseThrow();

        assertEquals(FACILITY_ID, counts.getFacilityId());
        assertEquals(REPORT_ID, counts.getReportId());
        assertNotNull(counts.getLastUpdated());
        assertEquals(List.of("uncategorized", "category-n", "category-a"), counts.getCategories()
                .stream()
                .map(CategoryCountModel::getCategoryId)
                .toList());
        CategoryCountModel notSubmitted = counts.getCategories().get(1);
        assertEquals("Title of category-n", notSubmitted.getTitle());
        assertTrue(notSubmitted.isAcceptable());
        assertFalse(notSubmitted.isSubmit());
        assertEquals(2L, notSubmitted.getIssues());
    }

    @Test
    void getReportCounts_filteredOnSubmit_returnsOnlyMatchingCategories() {
        Category a = category("category-a", true, false);
        Category n = category("category-n", false, false);
        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1",
                List.of(result(a), result(n), uncategorized()));

        assertEquals(List.of("category-a", "uncategorized"), categoryIds(true));
        assertEquals(List.of("category-n"), categoryIds(false));
    }

    @Test
    void getReportCounts_countedReportWithNoFindings_isPresentWithNoCategories() {
        categoryCountService.recordPatientCounts(FACILITY_ID, REPORT_ID, "patient-1", Collections.emptyList());

        ReportCategoryCountsModel counts = categoryCountService.getReportCounts(FACILITY_ID, REPORT_ID, null)
                .orElseThrow();

        assertTrue(counts.getCategories().isEmpty());
    }

    private List<String> categoryIds(boolean submit) {
        return categoryCountService.getReportCounts(FACILITY_ID, REPORT_ID, submit)
                .orElseThrow()
                .getCategories()
                .stream()
                .map(CategoryCountModel::getCategoryId)
                .toList();
    }

    private Map<String, Long> totals() {
        return reportCategoryCountRepository.findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID)
                .stream()
                .collect(Collectors.toMap(ReportCategoryCount::getCategoryId, ReportCategoryCount::getIssues));
    }

    private ReportCategoryCount total(String categoryId) {
        return reportCategoryCountRepository.findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID)
                .stream()
                .filter(total -> total.getCategoryId().equals(categoryId))
                .findFirst()
                .orElseThrow();
    }

    private Map<String, Long> patientCounts(String patientId) {
        return patientCategoryCountRepository.findAllByFacilityIdAndReportIdAndPatientId(
                        FACILITY_ID,
                        REPORT_ID,
                        patientId)
                .stream()
                .collect(Collectors.toMap(PatientCategoryCount::getCategoryId, PatientCategoryCount::getIssues));
    }

    private static Category category(String id, boolean submit, boolean acceptable) {
        Category category = new Category();
        category.setId(id);
        category.setTitle("Title of " + id);
        category.setSubmit(submit);
        category.setAcceptable(acceptable);
        return category;
    }

    private static Result result(Category... categories) {
        Result result = new Result();
        result.setCategories(List.of(categories));
        return result;
    }

    private static Result uncategorized() {
        Result result = new Result();
        result.setCategories(Collections.emptyList());
        return result;
    }
}
