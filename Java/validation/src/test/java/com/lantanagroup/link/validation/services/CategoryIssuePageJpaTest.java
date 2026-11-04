package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.*;
import jakarta.persistence.EntityManager;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import java.util.List;
import static org.junit.jupiter.api.Assertions.*;

@DataJpaTest(properties = {
    "spring.datasource.url=jdbc:h2:mem:issuepages;MODE=MSSQLServer;DB_CLOSE_DELAY=-1",
    "spring.datasource.driver-class-name=org.h2.Driver",
    "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import({ResultService.class, CategoryIssuePageJpaTest.Config.class})
class CategoryIssuePageJpaTest {
    @TestConfiguration
    static class Config {
        @Bean ObjectMapper objectMapper() { return new ObjectMapper(); }
    }

    @Autowired EntityManager em;
    @Autowired ResultService service;

    @Test
    void filtersBeforePagingAndReturnsStablePagesWithExactTotals() {
        Category a = category("a");
        Category b = category("b");
        result("facility", "report", "first", List.of(a, b));
        result("facility", "report", "second", List.of(a));
        result("other", "report", "wrong facility", List.of(a));
        result("facility", "other", "wrong report", List.of(a));
        result("facility", "report", "wrong category", List.of(b));
        em.flush();
        em.clear();
        var first = service.getCategoryIssuesPage("facility", "report", "a", 1, 1);
        var second = service.getCategoryIssuesPage("facility", "report", "a", 2, 1);
        assertEquals("first", first.records().get(0).getMessage());
        assertEquals("second", second.records().get(0).getMessage());
        assertEquals(2, first.metadata().totalCount());
        assertEquals(2, first.metadata().totalPages());
        assertEquals(2, second.metadata().pageNumber());
        var empty = service.getCategoryIssuesPage("facility", "report", "missing", 1, 25);
        assertTrue(empty.records().isEmpty());
        assertEquals(0, empty.metadata().totalCount());
    }

    @Test
    void uncategorizedIncludesOnlyRowsWithoutCategoryLinks() {
        result("facility", "report", "uncategorized", List.of());
        result("facility", "report", "categorized", List.of(category("a")));
        em.flush();
        em.clear();
        var page = service.getCategoryIssuesPage("facility", "report", "Uncategorized", 1, 25);
        assertEquals(1, page.metadata().totalCount());
        assertEquals("uncategorized", page.records().get(0).getMessage());
    }

    private Category category(String id) {
        Category c = new Category();
        c.setId(id); c.setTitle(id); c.setGuidance("guidance"); c.setSeverity(CategorySeverity.WARNING);
        em.persist(c);
        return c;
    }

    private void result(String facility, String report, String message, List<Category> categories) {
        Result r = new Result();
        r.setFacilityId(facility); r.setReportId(report); r.setPatientId("patient");
        r.setMessage(message); r.setSeverity(OperationOutcome.IssueSeverity.ERROR);
        r.setCode(OperationOutcome.IssueType.INVALID); r.setCategories(categories);
        em.persist(r);
    }
}
