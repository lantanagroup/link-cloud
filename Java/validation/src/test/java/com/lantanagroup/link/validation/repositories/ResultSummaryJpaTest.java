package com.lantanagroup.link.validation.repositories;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.utils.IssueSeverityUtils;
import com.lantanagroup.link.validation.entities.Result;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.boot.test.autoconfigure.orm.jpa.TestEntityManager;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;

import static org.junit.jupiter.api.Assertions.assertEquals;

@DataJpaTest(properties = {
        "spring.datasource.url=jdbc:h2:mem:resultsummary;MODE=MSSQLServer;DB_CLOSE_DELAY=-1",
        "spring.datasource.driver-class-name=org.h2.Driver",
        "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
class ResultSummaryJpaTest {

    @TestConfiguration
    static class Config {
        @Bean
        ObjectMapper objectMapper() {
            return new ObjectMapper();
        }
    }

    @Autowired
    private ResultRepository resultRepository;

    @Autowired
    private TestEntityManager testEntityManager;

    @Test
    void countBySeverityDoesNotIncludeInformationWhenThresholdIsWarning() {
        persist(OperationOutcome.IssueSeverity.ERROR);
        persist(OperationOutcome.IssueSeverity.WARNING);
        persist(OperationOutcome.IssueSeverity.INFORMATION);
        testEntityManager.flush();
        testEntityManager.clear();

        long count = resultRepository.countByFacilityIdAndReportIdAndSeverityIn(
                "facility",
                "report",
                IssueSeverityUtils.atLeastAsSevere(OperationOutcome.IssueSeverity.WARNING));

        assertEquals(2L, count);
    }

    private void persist(OperationOutcome.IssueSeverity severity) {
        Result result = new Result();
        result.setFacilityId("facility");
        result.setReportId("report");
        result.setPatientId("patient-" + severity.name());
        result.setSeverity(severity);
        result.setCode(OperationOutcome.IssueType.INVALID);
        result.setMessage("message for " + severity.name());
        testEntityManager.persist(result);
    }
}
