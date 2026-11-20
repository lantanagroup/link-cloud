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
import org.hl7.fhir.r4.model.Reference;
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

    static final String PQ_ISSUE_CAT_URL = "http://hl7.org/fhir/us/core/StructureDefinition/pq-issue-cat";
    static final String PQ_ISSUE_PROFILE_URL = "http://nhsnlink.org/pq-issue-profile";
    static final String OO_TOTAL_URL = "http://nhsnlink.org/oo-total";
    /** R4 OperationOutcome declares only {@code issue}, so an extension is the only place for the patient. */
    static final String PQ_QUALIFYING_PATIENT_URL = "http://nhsnlink.org/pq-qualifying-patient";
    /** Entry position and id, the shape every expression here takes. {@link InstanceAnchoredExpression}. */
    static final String MEASURE_REPORT_LOCATOR =
            "Bundle.entry[%d].resource.ofType(MeasureReport).where(id = '%s').extension[0]";

    public record MeasureReportRef(int index, String id) {
    }

    private record IssueKey(String categoryId, String profile) {
    }

    /**
     * @return empty when no finding maps to a submitted category. {@code measureReport} and
     * {@code patientId} may be null.
     */
    public Optional<OperationOutcome> build(
            List<Result> results, MeasureReportRef measureReport, String patientId,
            boolean writeExpressions) {
        // Keyed by category id, not the Category entity: Category defines no equals/hashCode, so two
        // instances of one logical category would group separately and duplicate its issue, inflating
        // oo-total. LinkedHashMap keeps issue order deterministic.
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
                // The id is checked as well as the ref: a null id formats to where(id = 'null'), valid
                // FHIRPath that silently selects nothing. Omitting the locator is the honest outcome.
                if (measureReport != null && measureReport.id() != null) {
                    issue.addExpression(String.format(
                            MEASURE_REPORT_LOCATOR, measureReport.index(), measureReport.id()));
                } else {
                    _logger.warn("No identifiable MeasureReport in the bundle; omitting the MeasureReport locator expression");
                }
                categoryResults.forEach(r -> issue.addExpression(r.getExpression()));
            }
        }

        if (patientId != null) {
            operationOutcome.addExtension(
                    new Extension(PQ_QUALIFYING_PATIENT_URL, new Reference("Patient/" + patientId)));
        } else {
            _logger.warn("No patient id available; omitting the pq-qualifying-patient extension");
        }
        operationOutcome.addExtension(new Extension(OO_TOTAL_URL, new IntegerType(operationOutcome.getIssue().size())));

        return Optional.of(operationOutcome);
    }

    /** The first MeasureReport's entry position and id, or null when the bundle holds none. */
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
