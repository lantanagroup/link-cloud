package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategorySeverity;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

/**
 * Runs with real transactions (the test itself is not transactional), so a failed save can be seen to roll back the
 * delete that came before it.
 */
@DataJpaTest(properties = {
        "spring.datasource.url=jdbc:h2:mem:resultservice;MODE=MSSQLServer;DB_CLOSE_DELAY=-1",
        "spring.datasource.driver-class-name=org.h2.Driver",
        "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Transactional(propagation = Propagation.NOT_SUPPORTED)
@Import(ResultService.class)
class ResultServiceJpaTest {
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
    private ResultService resultService;

    @Autowired
    private ResultRepository resultRepository;

    @Autowired
    private CategoryRepository categoryRepository;

    @Autowired
    private JdbcTemplate jdbcTemplate;

    private Category category;

    @BeforeEach
    void setUp() {
        category = new Category();
        category.setId("category-a");
        category.setTitle("Category A");
        category.setSeverity(CategorySeverity.WARNING);
        category.setGuidance("Fix it.");
        category = categoryRepository.save(category);
    }

    @AfterEach
    void cleanUp() {
        jdbcTemplate.execute("DELETE FROM result_category");
        resultRepository.deleteAll();
        categoryRepository.deleteAll();
    }

    @Test
    void replacePatientResults_calledAgain_leavesOnlyTheNewResults() {
        resultService.replacePatientResults(FACILITY_ID, REPORT_ID, "patient-1",
                List.of(result("patient-1", "first"), result("patient-1", "second")));

        resultService.replacePatientResults(FACILITY_ID, REPORT_ID, "patient-1", List.of(result("patient-1", "third")));

        assertEquals(List.of("third"), messages(REPORT_ID, "patient-1"));
        assertEquals(1, categoryLinks());
    }

    @Test
    void replacePatientResults_leavesOtherPatientsAndReportsAlone() {
        resultService.replacePatientResults(FACILITY_ID, REPORT_ID, "patient-2", List.of(result("patient-2", "other")));
        resultService.replacePatientResults(FACILITY_ID, "report-2", "patient-1",
                List.of(result("report-2", "patient-1", "other report")));
        resultService.replacePatientResults(FACILITY_ID, REPORT_ID, "patient-1", List.of(result("patient-1", "first")));

        resultService.replacePatientResults(FACILITY_ID, REPORT_ID, "patient-1", List.of(result("patient-1", "second")));

        assertEquals(List.of("other"), messages(REPORT_ID, "patient-2"));
        assertEquals(List.of("other report"), messages("report-2", "patient-1"));
        assertEquals(List.of("second"), messages(REPORT_ID, "patient-1"));
    }

    @Test
    void replacePatientResults_emptyList_removesThePatientsResults() {
        resultService.replacePatientResults(FACILITY_ID, REPORT_ID, "patient-1", List.of(result("patient-1", "first")));

        resultService.replacePatientResults(FACILITY_ID, REPORT_ID, "patient-1", List.of());

        assertEquals(List.of(), messages(REPORT_ID, "patient-1"));
        assertEquals(0, categoryLinks());
    }

    @Test
    void replacePatientResults_whenSavingFails_keepsTheEarlierResults() {
        resultService.replacePatientResults(FACILITY_ID, REPORT_ID, "patient-1", List.of(result("patient-1", "first")));
        Result invalid = result("patient-1", "invalid");
        invalid.setMessage(null);

        assertThrows(RuntimeException.class, () -> resultService.replacePatientResults(
                FACILITY_ID, REPORT_ID, "patient-1", List.of(invalid)));

        assertEquals(List.of("first"), messages(REPORT_ID, "patient-1"));
        assertEquals(1, categoryLinks());
    }

    private Result result(String patientId, String message) {
        return result(REPORT_ID, patientId, message);
    }

    private Result result(String reportId, String patientId, String message) {
        Result result = new Result();
        result.setFacilityId(FACILITY_ID);
        result.setReportId(reportId);
        result.setPatientId(patientId);
        result.setSeverity(OperationOutcome.IssueSeverity.ERROR);
        result.setCode(OperationOutcome.IssueType.INVALID);
        result.setMessage(message);
        result.setCategories(List.of(category));
        return result;
    }

    private List<String> messages(String reportId, String patientId) {
        return resultRepository.findAllByFacilityIdAndReportIdAndPatientId(FACILITY_ID, reportId, patientId)
                .stream()
                .map(Result::getMessage)
                .sorted()
                .toList();
    }

    private int categoryLinks() {
        Integer links = jdbcTemplate.queryForObject("SELECT COUNT(*) FROM result_category", Integer.class);
        return links == null ? 0 : links;
    }
}
