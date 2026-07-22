package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.CodeType;
import org.hl7.fhir.r4.model.CodeableConcept;
import org.hl7.fhir.r4.model.Extension;
import org.hl7.fhir.r4.model.IntegerType;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.hl7.fhir.r4.model.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;

/**
 * Builds the single pre-qualification {@link OperationOutcome} written to the patient NDJSON: one issue
 * per <em>unacceptable</em> category (a category with {@code acceptable=false}) that has at least one
 * finding. See LEGLINK-425.
 */
@Component
public class PreQualOperationOutcomeBuilder {
    private final Logger _logger = LoggerFactory.getLogger(PreQualOperationOutcomeBuilder.class);

    /** Extension URL carrying the category id on each issue. */
    static final String PQ_ISSUE_CAT_URL = "http://hl7.org/fhir/us/core/StructureDefinition/pq-issue-cat";
    /** Extension URL carrying the total issue count on the OperationOutcome. */
    static final String OO_TOTAL_URL = "http://nhsnlink.org/oo-total";
    /**
     * Locator expression anchoring each issue to the patient's MeasureReport
     * ({@code %d} = its Bundle entry index, {@code %s} = its id). The index must be the MeasureReport's
     * actual position in the bundle: the aggregator happens to write it first today, but a hard-coded
     * {@code entry[0]} silently resolves to nothing as soon as that ordering changes.
     */
    static final String MEASURE_REPORT_LOCATOR =
            "Bundle.entry[%d].resource.ofType(MeasureReport).where(id = '%s').extension[0]";

    /**
     * A MeasureReport located in the submission bundle: its entry index and its id.
     */
    public record MeasureReportRef(int index, String id) {
    }

    /**
     * Builds the OperationOutcome for a patient's categorized findings.
     *
     * @param results          the patient's categorized validation results
     * @param measureReport    the patient's MeasureReport in the submission bundle (may be null)
     * @param writeExpressions when false, {@code expression[]} is omitted from every issue
     * @return the OperationOutcome, or {@link Optional#empty()} when no unacceptable-category findings exist
     */
    public Optional<OperationOutcome> build(List<Result> results, MeasureReportRef measureReport, boolean writeExpressions) {
        // Group findings by unacceptable category (acceptable == false); a Result may map to several
        // categories. LinkedHashMap keeps issue order deterministic (first-seen category order).
        Map<Category, List<Result>> byCategory = new LinkedHashMap<>();
        for (Result result : results) {
            List<Category> categories = result.getCategories();
            if (categories == null) {
                continue;
            }
            for (Category category : categories) {
                if (!category.isAcceptable()) {
                    byCategory.computeIfAbsent(category, c -> new java.util.ArrayList<>()).add(result);
                }
            }
        }

        if (byCategory.isEmpty()) {
            return Optional.empty();
        }

        OperationOutcome operationOutcome = new OperationOutcome();

        for (Map.Entry<Category, List<Result>> entry : byCategory.entrySet()) {
            Category category = entry.getKey();
            List<Result> categoryResults = entry.getValue();

            OperationOutcome.OperationOutcomeIssueComponent issue = operationOutcome.addIssue()
                    .setSeverity(OperationOutcome.IssueSeverity.ERROR)
                    .setCode(OperationOutcome.IssueType.PROCESSING)
                    .setDetails(new CodeableConcept().setText(categoryResults.get(0).getMessage()));

            issue.addExtension(new Extension(PQ_ISSUE_CAT_URL, new CodeType(category.getId())));

            if (writeExpressions) {
                if (measureReport != null) {
                    issue.addExpression(
                            String.format(MEASURE_REPORT_LOCATOR, measureReport.index(), measureReport.id()));
                } else {
                    _logger.warn("No MeasureReport in the bundle; omitting the MeasureReport locator expression");
                }
                categoryResults.forEach(r -> issue.addExpression(r.getExpression()));
            }
        }

        operationOutcome.addExtension(new Extension(OO_TOTAL_URL, new IntegerType(operationOutcome.getIssue().size())));

        return Optional.of(operationOutcome);
    }

    /**
     * Locates the first MeasureReport anywhere in the submission bundle, returning both its entry index
     * and its id, or null if the bundle contains none. The index is carried alongside the id because the
     * locator expression addresses the MeasureReport by position; scanning for the id while assuming
     * position 0 would emit an expression that resolves to nothing.
     */
    public MeasureReportRef resolveMeasureReport(Bundle bundle) {
        if (bundle == null) {
            return null;
        }
        List<Bundle.BundleEntryComponent> entries = bundle.getEntry();
        for (int index = 0; index < entries.size(); index++) {
            Resource resource = entries.get(index).getResource();
            if (resource instanceof MeasureReport) {
                return new MeasureReportRef(index, resource.getIdElement().getIdPart());
            }
        }
        return null;
    }
}
