package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.*;
import jakarta.persistence.EntityManager;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import java.io.StringWriter;
import java.util.List;
import static org.junit.jupiter.api.Assertions.*;

@DataJpaTest(showSql = false, properties = {
    "spring.datasource.url=jdbc:h2:mem:csvexport;MODE=MSSQLServer;DB_CLOSE_DELAY=-1",
    "spring.datasource.driver-class-name=org.h2.Driver", "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import({ResultCsvExportService.class, ObjectMapper.class})
class ResultCsvExportServiceJpaTest {
    @Autowired EntityManager em;
    @Autowired ResultCsvExportService service;

    @Test
    void exportsAcrossBatchBoundaryWithoutLosingMultipleCategoriesOrUncategorizedFindings() throws Exception {
        Category a = category("a");
        Category b = category("b");
        for (int i = 0; i < 1001; i++) result("report", "message-" + i, i == 0 ? List.of(a, b) : List.of());
        result("other-report", "excluded", List.of());
        em.flush();
        Long upper = service.upperBound("facility", "report");
        result("report", "inserted-after-start", List.of());
        em.flush();
        StringWriter csv = new StringWriter();
        service.write("facility", "report", upper, csv);
        List<String> lines = csv.toString().lines().toList();
        assertEquals(1003, lines.size()); // header + 1001 findings + second category for first finding
        assertTrue(lines.get(1).contains("\"a\",\"a\""));
        assertTrue(lines.get(2).contains("\"b\",\"b\""));
        assertTrue(csv.toString().contains("message-1000"));
        assertFalse(csv.toString().contains("excluded"));
        assertFalse(csv.toString().contains("inserted-after-start"));
    }

    @Test
    void exportsBooleanFlagsAsTextAndLeavesUncategorizedFlagsBlank() throws Exception {
        Category category = category("flags");
        category.setAcceptable(true);
        category.setSubmit(false);
        result("report", "categorized", List.of(category));
        result("report", "uncategorized", List.of());
        em.flush();

        StringWriter csv = new StringWriter();
        service.write("facility", "report", service.upperBound("facility", "report"), csv);
        List<String> lines = csv.toString().lines().toList();
        assertEquals(3, lines.size());
        assertTrue(lines.get(1).endsWith("\"true\",\"false\",\"guidance\""));
        assertTrue(lines.get(2).endsWith(",\"\",\"\",\"\",\"\",\"\",\"\""));
    }

    @Test
    void scopesUpperBoundAndRowsToFacilityAndReport() throws Exception {
        result("report", "included", List.of());
        em.flush();
        Long expectedUpper = service.upperBound("facility", "report");
        result("other-facility", "report", "excluded-facility", List.of());
        result("other-report", "excluded-report", List.of());
        em.flush();

        assertEquals(expectedUpper, service.upperBound("facility", "report"));
        assertNull(service.upperBound("missing-facility", "report"));
        StringWriter csv = new StringWriter();
        service.write("facility", "report", service.upperBound("other-facility", "report"), csv);
        assertEquals(2, csv.toString().lines().count());
        assertTrue(csv.toString().contains("included"));
        assertFalse(csv.toString().contains("excluded"));
    }

    @Test
    void emptyReportIsHeaderOnly() throws Exception {
        StringWriter csv = new StringWriter();
        service.write("facility", "missing", service.upperBound("facility", "missing"), csv);
        assertEquals(1, csv.toString().lines().count());
        assertTrue(csv.toString().startsWith("resultId,facilityId,reportId,patientId"));
    }

    @Test
    void quotesCommasNewlinesAndFormulaPrefixes() throws Exception {
        StringWriter cell = new StringWriter();
        ResultCsvExportService.writeCell(cell, "hello, \"world\"\nnext");
        assertEquals("\"hello, \"\"world\"\"\nnext\"", cell.toString());
        cell = new StringWriter();
        ResultCsvExportService.writeCell(cell, " =SUM(A1:A2)");
        assertEquals("\"' =SUM(A1:A2)\"", cell.toString());
    }

    @Test
    void propagatesDisconnectWhileWritingFindingRows() throws Exception {
        result("report", "finding", List.of());
        em.flush();
        var writer = new java.io.Writer() {
            boolean headerWritten;
            public void write(char[] chars, int offset, int length) throws java.io.IOException {
                if (headerWritten) throw new java.io.IOException("Client disconnected");
                headerWritten = true;
            }
            public void flush() {}
            public void close() {}
        };
        var error = assertThrows(java.io.IOException.class,
                () -> service.write("facility", "report", service.upperBound("facility", "report"), writer));
        assertEquals("Client disconnected", error.getMessage());
    }

    private Category category(String id) {
        Category c = new Category();
        c.setId(id); c.setTitle(id); c.setGuidance("guidance"); c.setSeverity(CategorySeverity.WARNING);
        em.persist(c); return c;
    }
    private void result(String report, String message, List<Category> categories) {
        result("facility", report, message, categories);
    }
    private void result(String facility, String report, String message, List<Category> categories) {
        Result r = new Result();
        r.setFacilityId(facility); r.setReportId(report); r.setPatientId("patient");
        r.setMessage(message); r.setSeverity(OperationOutcome.IssueSeverity.ERROR);
        r.setCode(OperationOutcome.IssueType.INVALID); r.setCategories(categories);
        em.persist(r);
    }
}
