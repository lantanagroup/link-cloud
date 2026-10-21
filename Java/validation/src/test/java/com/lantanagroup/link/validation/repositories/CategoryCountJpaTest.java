package com.lantanagroup.link.validation.repositories;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.PatientCategoryCount;
import com.lantanagroup.link.validation.entities.ReportCategoryCount;
import com.lantanagroup.link.validation.entities.ReportCategoryCountStatus;
import jakarta.persistence.PersistenceException;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.boot.test.autoconfigure.orm.jpa.TestEntityManager;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;

@DataJpaTest(properties = {
        "spring.datasource.url=jdbc:h2:mem:categorycount;MODE=MSSQLServer;DB_CLOSE_DELAY=-1",
        "spring.datasource.driver-class-name=org.h2.Driver",
        "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
class CategoryCountJpaTest {

    @TestConfiguration
    static class Config {
        @Bean
        ObjectMapper objectMapper() {
            return new ObjectMapper();
        }
    }

    @Autowired
    private ReportCategoryCountRepository reportCategoryCountRepository;

    @Autowired
    private PatientCategoryCountRepository patientCategoryCountRepository;

    @Autowired
    private ReportCategoryCountStatusRepository reportCategoryCountStatusRepository;

    @Autowired
    private TestEntityManager testEntityManager;

    @Test
    void reportTotalsAreFoundForTheirReportOnly() {
        testEntityManager.persist(reportCount("report-1", "category-a", 3));
        testEntityManager.persist(reportCount("report-1", "category-b", 1));
        testEntityManager.persist(reportCount("report-2", "category-a", 7));
        testEntityManager.flush();
        testEntityManager.clear();

        List<ReportCategoryCount> counts = reportCategoryCountRepository
                .findAllByFacilityIdAndReportId("facility", "report-1");

        assertEquals(2, counts.size());
        assertEquals(4L, counts.stream()
                .mapToLong(ReportCategoryCount::getIssues)
                .sum());
    }

    @Test
    void reportTotalIsUniquePerCategory() {
        testEntityManager.persistAndFlush(reportCount("report-1", "category-a", 3));

        assertThrows(PersistenceException.class,
                () -> testEntityManager.persistAndFlush(reportCount("report-1", "category-a", 1)));
    }

    @Test
    void patientCountsAreFoundForTheirPatientOnly() {
        testEntityManager.persist(patientCount("patient-1", "category-a", 2));
        testEntityManager.persist(patientCount("patient-1", "category-b", 5));
        testEntityManager.persist(patientCount("patient-2", "category-a", 1));
        testEntityManager.flush();
        testEntityManager.clear();

        List<PatientCategoryCount> counts = patientCategoryCountRepository
                .findAllByFacilityIdAndReportIdAndPatientId("facility", "report-1", "patient-1");

        assertEquals(2, counts.size());
    }

    @Test
    void patientCountIsUniquePerCategory() {
        testEntityManager.persistAndFlush(patientCount("patient-1", "category-a", 2));

        assertThrows(PersistenceException.class,
                () -> testEntityManager.persistAndFlush(patientCount("patient-1", "category-a", 4)));
    }

    @Test
    void statusIsUniquePerReport() {
        testEntityManager.persistAndFlush(status("report-1", 1));

        assertThrows(PersistenceException.class,
                () -> testEntityManager.persistAndFlush(status("report-1", 2)));
    }

    @Test
    void lockedStatusLookupReturnsTheReportsRow() {
        testEntityManager.persist(status("report-1", 12));
        testEntityManager.persist(status("report-2", 3));
        testEntityManager.flush();
        testEntityManager.clear();

        Optional<ReportCategoryCountStatus> status = reportCategoryCountStatusRepository
                .findWithLockByFacilityIdAndReportId("facility", "report-1");

        assertTrue(status.isPresent());
        assertEquals(12, status.get().getPatientsCounted());
    }

    @Test
    void statusLookupIsEmptyForAReportWithNoCounts() {
        Optional<ReportCategoryCountStatus> status = reportCategoryCountStatusRepository
                .findByFacilityIdAndReportId("facility", "report-missing");

        assertTrue(status.isEmpty());
    }

    private ReportCategoryCount reportCount(String reportId, String categoryId, long issues) {
        ReportCategoryCount count = new ReportCategoryCount();
        count.setFacilityId("facility");
        count.setReportId(reportId);
        count.setCategoryId(categoryId);
        count.setTitle("Title of " + categoryId);
        count.setAcceptable(false);
        count.setIssues(issues);
        return count;
    }

    private PatientCategoryCount patientCount(String patientId, String categoryId, long issues) {
        PatientCategoryCount count = new PatientCategoryCount();
        count.setFacilityId("facility");
        count.setReportId("report-1");
        count.setPatientId(patientId);
        count.setCategoryId(categoryId);
        count.setIssues(issues);
        return count;
    }

    private ReportCategoryCountStatus status(String reportId, int patientsCounted) {
        ReportCategoryCountStatus status = new ReportCategoryCountStatus();
        status.setFacilityId("facility");
        status.setReportId(reportId);
        status.setPatientsCounted(patientsCounted);
        status.setLastUpdated(OffsetDateTime.now());
        return status;
    }
}
