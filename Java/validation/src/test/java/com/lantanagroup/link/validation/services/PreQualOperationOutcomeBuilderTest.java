package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.CodeType;
import org.hl7.fhir.r4.model.IntegerType;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.hl7.fhir.r4.model.StringType;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;

class PreQualOperationOutcomeBuilderTest {

    private static final String MEASURE_REPORT_ID = "mr-1";
    private static final PreQualOperationOutcomeBuilder.MeasureReportRef MEASURE_REPORT =
            new PreQualOperationOutcomeBuilder.MeasureReportRef(0, MEASURE_REPORT_ID);

    private PreQualOperationOutcomeBuilder builder;

    @BeforeEach
    void setUp() {
        builder = new PreQualOperationOutcomeBuilder();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private Category category(String id, boolean acceptable) {
        Category category = new Category();
        category.setId(id);
        category.setAcceptable(acceptable);
        return category;
    }

    private Result result(String message, String expression, Category... categories) {
        Result result = new Result();
        result.setMessage(message);
        result.setExpression(expression);
        result.setCategories(new ArrayList<>(Arrays.asList(categories)));
        return result;
    }

    private List<String> expressionStrings(OperationOutcome.OperationOutcomeIssueComponent issue) {
        return issue.getExpression().stream().map(StringType::getValue).toList();
    }

    // -------------------------------------------------------------------------
    // Grouping / issue content
    // -------------------------------------------------------------------------

    @Test
    void build_oneIssuePerUnacceptableCategory_withOoTotal() {
        Result r1 = result("Code is inactive.", "expr1", category("inactive_code", false));
        Result r2 = result("Unable to validate.", "expr2", category("unable_to_validate_code", false));

        OperationOutcome oo = builder.build(List.of(r1, r2), MEASURE_REPORT, true).orElseThrow();

        assertEquals(2, oo.getIssue().size());
        int total = ((IntegerType) oo.getExtensionByUrl(PreQualOperationOutcomeBuilder.OO_TOTAL_URL).getValue()).getValue();
        assertEquals(2, total);
    }

    @Test
    void build_issueCarriesSeverityCodeMessageAndCategoryExtension() {
        Result r1 = result("Code is inactive.", "expr1", category("inactive_code", false));

        OperationOutcome oo = builder.build(List.of(r1), MEASURE_REPORT, true).orElseThrow();
        OperationOutcome.OperationOutcomeIssueComponent issue = oo.getIssueFirstRep();

        assertEquals(OperationOutcome.IssueSeverity.ERROR, issue.getSeverity());
        assertEquals(OperationOutcome.IssueType.PROCESSING, issue.getCode());
        assertEquals("Code is inactive.", issue.getDetails().getText());
        CodeType cat = (CodeType) issue.getExtensionByUrl(PreQualOperationOutcomeBuilder.PQ_ISSUE_CAT_URL).getValue();
        assertEquals("inactive_code", cat.getValue());
    }

    @Test
    void build_detailsTextUsesFirstResultMessageForCategory() {
        Category shared = category("inactive_code", false);
        Result first = result("first message", "expr1", shared);
        Result second = result("second message", "expr2", shared);

        OperationOutcome oo = builder.build(List.of(first, second), MEASURE_REPORT, true).orElseThrow();

        assertEquals(1, oo.getIssue().size());
        assertEquals("first message", oo.getIssueFirstRep().getDetails().getText());
    }

    @Test
    void build_oneResultMappingToMultipleUnacceptableCategories_producesAnIssuePerCategory() {
        Result r = result("msg", "expr", category("cat_a", false), category("cat_b", false));

        OperationOutcome oo = builder.build(List.of(r), MEASURE_REPORT, true).orElseThrow();

        assertEquals(2, oo.getIssue().size());
    }

    @Test
    void build_excludesAcceptableCategories() {
        Result unacceptable = result("bad", "expr1", category("inactive_code", false));
        Result acceptable = result("ok", "expr2", category("incorrect_display_value_for_code", true));

        OperationOutcome oo = builder.build(List.of(unacceptable, acceptable), MEASURE_REPORT, true).orElseThrow();

        assertEquals(1, oo.getIssue().size());
        CodeType cat = (CodeType) oo.getIssueFirstRep()
                .getExtensionByUrl(PreQualOperationOutcomeBuilder.PQ_ISSUE_CAT_URL).getValue();
        assertEquals("inactive_code", cat.getValue());
    }

    @Test
    void build_noUnacceptableFindings_returnsEmpty() {
        Result acceptable = result("ok", "expr", category("incorrect_display_value_for_code", true));

        assertTrue(builder.build(List.of(acceptable), MEASURE_REPORT, true).isEmpty());
    }

    @Test
    void build_resultWithNullCategories_isIgnored() {
        Result result = new Result();
        result.setCategories(null);

        assertTrue(builder.build(List.of(result), MEASURE_REPORT, true).isEmpty());
    }

    // -------------------------------------------------------------------------
    // Expressions (WriteExpressionsInOperationOutcome)
    // -------------------------------------------------------------------------

    @Test
    void build_writeExpressionsTrue_addsMeasureReportLocatorThenResultExpressions() {
        Category shared = category("inactive_code", false);
        Result r1 = result("msg1", "Bundle.entry[14].resource.ofType(Condition).code.coding[0]", shared);
        Result r2 = result("msg2", "Bundle.entry[27].resource.ofType(Observation).code.coding[0]", shared);

        OperationOutcome oo = builder.build(List.of(r1, r2), MEASURE_REPORT, true).orElseThrow();

        List<String> expressions = expressionStrings(oo.getIssueFirstRep());
        assertEquals(3, expressions.size());
        assertEquals(String.format(PreQualOperationOutcomeBuilder.MEASURE_REPORT_LOCATOR, 0, MEASURE_REPORT_ID),
                expressions.get(0));
        assertEquals("Bundle.entry[14].resource.ofType(Condition).code.coding[0]", expressions.get(1));
        assertEquals("Bundle.entry[27].resource.ofType(Observation).code.coding[0]", expressions.get(2));
    }

    @Test
    void build_writeExpressionsFalse_omitsAllExpressions() {
        Result r1 = result("msg", "expr", category("inactive_code", false));

        OperationOutcome oo = builder.build(List.of(r1), MEASURE_REPORT, false).orElseThrow();

        assertTrue(oo.getIssueFirstRep().getExpression().isEmpty());
    }

    @Test
    void build_nullMeasureReportId_omitsLocatorButKeepsResultExpressions() {
        Result r1 = result("msg", "result-expr", category("inactive_code", false));

        OperationOutcome oo = builder.build(List.of(r1), null, true).orElseThrow();

        List<String> expressions = expressionStrings(oo.getIssueFirstRep());
        assertEquals(List.of("result-expr"), expressions);
    }

    // -------------------------------------------------------------------------
    // MeasureReport resolution (index + id)
    // -------------------------------------------------------------------------

    @Test
    void resolveMeasureReport_returnsIndexAndIdWhenFirstEntry() {
        Bundle bundle = new Bundle();
        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("report-abc");
        bundle.addEntry().setResource(measureReport);

        PreQualOperationOutcomeBuilder.MeasureReportRef ref = builder.resolveMeasureReport(bundle);

        assertNotNull(ref);
        assertEquals(0, ref.index());
        assertEquals("report-abc", ref.id());
    }

    @Test
    void resolveMeasureReport_findsMeasureReportAtNonZeroEntryIndex() {
        // Regression: the locator used to hard-code Bundle.entry[0]. The aggregator happens to write the
        // MeasureReport first today, but if it is anywhere else the locator must follow it.
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(new org.hl7.fhir.r4.model.Patient());
        bundle.addEntry().setResource(new org.hl7.fhir.r4.model.Encounter());
        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("report-xyz");
        bundle.addEntry().setResource(measureReport);

        PreQualOperationOutcomeBuilder.MeasureReportRef ref = builder.resolveMeasureReport(bundle);

        assertNotNull(ref);
        assertEquals(2, ref.index());
        assertEquals("report-xyz", ref.id());
    }

    @Test
    void build_measureReportAtNonZeroIndex_locatorUsesThatIndex() {
        Result r1 = result("msg", "result-expr", category("inactive_code", false));
        PreQualOperationOutcomeBuilder.MeasureReportRef ref =
                new PreQualOperationOutcomeBuilder.MeasureReportRef(3, "report-xyz");

        OperationOutcome oo = builder.build(List.of(r1), ref, true).orElseThrow();

        String locator = expressionStrings(oo.getIssueFirstRep()).get(0);
        assertEquals(
                "Bundle.entry[3].resource.ofType(MeasureReport).where(id = 'report-xyz').extension[0]",
                locator);
        assertFalse(locator.contains("entry[0]"), "Locator must not fall back to entry[0]");
    }

    @Test
    void resolveMeasureReport_returnsNullWhenNoMeasureReport() {
        Bundle bundle = new Bundle();
        bundle.addEntry().setResource(new org.hl7.fhir.r4.model.Patient());

        assertNull(builder.resolveMeasureReport(bundle));
    }

    @Test
    void resolveMeasureReport_nullBundle_returnsNull() {
        assertNull(builder.resolveMeasureReport(null));
    }

    @Test
    void build_emptyResults_returnsEmpty() {
        assertTrue(builder.build(Collections.emptyList(), MEASURE_REPORT, true).isEmpty());
    }
}
