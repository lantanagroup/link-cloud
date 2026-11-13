package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.CanonicalType;
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

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;

/**
 * Builds the single pre-qualification {@link OperationOutcome} written to the patient NDJSON: one issue
 * per <em>submitted</em> category (a category with {@code submit=true}) and source profile that has at
 * least one finding. See LEGLINK-425 and LEGLINK-1172.
 */
@Component
public class PreQualOperationOutcomeBuilder {
    private final Logger _logger = LoggerFactory.getLogger(PreQualOperationOutcomeBuilder.class);

    /** Extension URL carrying the category id on each issue. */
    static final String PQ_ISSUE_CAT_URL = "http://hl7.org/fhir/us/core/StructureDefinition/pq-issue-cat";
    /** Extension URL carrying the source profile canonical on each issue; omitted for findings with no profile. */
    static final String PQ_ISSUE_PROFILE_URL = "http://nhsnlink.org/pq-issue-profile";
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
     * Groups findings into issues. {@code profile} is null for findings that belong to no profile; records
     * compare components with {@code Objects.equals}, so those still group together.
     */
    private record IssueKey(String categoryId, String profile) {
    }

    /**
     * Builds the OperationOutcome for a patient's categorized findings.
     *
     * @param results          the patient's categorized validation results
     * @param measureReport    the patient's MeasureReport in the submission bundle (may be null)
     * @param writeExpressions when false, {@code expression[]} is omitted from every issue
         * @return the OperationOutcome, or {@link Optional#empty()} when no submitted-category findings exist
     */
    public Optional<OperationOutcome> build(List<Result> results, MeasureReportRef measureReport, boolean writeExpressions) {
                // Group findings by submitted category (submit == true) and source profile; a Result may map to
        // several categories. Keyed by category id, not by the Category entity: Category defines no
        // equals/hashCode, so two instances of the same logical category (loaded in different
        // persistence contexts, say) would otherwise land in separate groups and emit a duplicate issue
        // for that category, inflating oo-total. The profile is part of the key so that findings from,
        // say, a lab and a vitals Observation in the same category stay distinguishable. LinkedHashMap
        // keeps issue order deterministic (first-seen order).
        Map<IssueKey, List<Result>> byIssueKey = new LinkedHashMap<>();
        for (Result result : results) {
            List<Category> categories = result.getCategories();
            if (categories == null) {
                continue;
            }
            for (Category category : categories) {
                if (category.isSubmit()) {
                    byIssueKey.computeIfAbsent(new IssueKey(category.getId(), result.getProfile()), key -> new ArrayList<>())
                            .add(result);
                }
            }
        }

        if (byIssueKey.isEmpty()) {
            return Optional.empty();
        }

        OperationOutcome operationOutcome = new OperationOutcome();
        operationOutcome.setId(UUID.randomUUID().toString());

        for (Map.Entry<IssueKey, List<Result>> entry : byIssueKey.entrySet()) {
            IssueKey key = entry.getKey();
            List<Result> categoryResults = entry.getValue();

            OperationOutcome.OperationOutcomeIssueComponent issue = operationOutcome.addIssue()
                    .setSeverity(OperationOutcome.IssueSeverity.ERROR)
                    .setCode(OperationOutcome.IssueType.PROCESSING)
                    .setDetails(new CodeableConcept().setText(categoryResults.get(0).getMessage()));

            issue.addExtension(new Extension(PQ_ISSUE_CAT_URL, new CodeType(key.categoryId())));
            if (key.profile() != null) {
                issue.addExtension(new Extension(PQ_ISSUE_PROFILE_URL, new CanonicalType(key.profile())));
            }

            if (writeExpressions) {
                // The id is checked as well as the ref: a MeasureReport carrying no id yields a non-null
                // ref with a null id, and formatting that produces where(id = 'null') — valid FHIRPath
                // that silently resolves to nothing. Omitting the locator is the honest outcome.
                if (measureReport != null && measureReport.id() != null) {
                    issue.addExpression(
                            String.format(MEASURE_REPORT_LOCATOR, measureReport.index(), measureReport.id()));
                } else {
                    _logger.warn("No identifiable MeasureReport in the bundle; omitting the MeasureReport locator expression");
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
