package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.utils.IssueSeverityUtils;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.models.CategoryIssueModel;
import com.lantanagroup.link.validation.models.CategorySummaryModel;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.springframework.stereotype.Service;

import java.util.List;

@Service
public class ResultService {
    private final ResultRepository resultRepository;

    public ResultService(ResultRepository resultRepository) {
        this.resultRepository = resultRepository;
    }

    private static final String UNCATEGORIZED_CATEGORY_ID = "uncategorized";

    /**
     * Gets results for a facility and report; optionally filters by a minimum severity
     *
     * @param facilityId The facility ID
     * @param reportId   The report ID
     * @param severity   The minimum severity threshold
     * @return List of filtered Result objects
     */
    public List<Result> getReportResults(String facilityId, String reportId, OperationOutcome.IssueSeverity severity) {
        return resultRepository.findAllByFacilityIdAndReportId(facilityId, reportId).stream()
                .filter(result -> IssueSeverityUtils.isAsSevere(result.getSeverity(), severity))
                .toList();
    }

    /**
     * Gets results for a facility, report, and patient; optionally filters by a minimum severity
     *
     * @param facilityId The facility ID
     * @param reportId   The report ID
     * @param patientId  The patient ID
     * @param severity   The minimum severity threshold
     * @return List of filtered Result objects
     */
    public List<Result> getReportPatientResults(String facilityId, String reportId, String patientId, 
                                               OperationOutcome.IssueSeverity severity) {
        return resultRepository.findAllByFacilityIdAndReportIdAndPatientId(facilityId, reportId, patientId).stream()
                .filter(result -> IssueSeverityUtils.isAsSevere(result.getSeverity(), severity))
                .toList();
    }

    /**
     * Gets categories that have issues associated with them for a facility and report
     *
     * @param facilityId The facility ID
     * @param reportId   The report ID
     * @return List of CategorySummaryDTO objects
     */
    public List<CategorySummaryModel> getReportCategories(String facilityId, String reportId) {
        List<Result> results = resultRepository.findAllByFacilityIdAndReportId(facilityId, reportId);

        // Count results without categories for the Uncategorized group
        long uncategorizedCount = results.stream()
                .filter(result -> result.getCategories() == null || result.getCategories().isEmpty())
                .count();

        // Group results by category and count issues
        var categories = results.stream()
                .filter(result -> result.getCategories() != null && !result.getCategories().isEmpty())
                .flatMap(result -> result.getCategories()
                        .stream()
                        .map(category -> new CategorySummaryModel(category.getId(), 1, category.isAcceptable())));

        var groupedCategories = categories
                .collect(java.util.stream.Collectors.groupingBy(
                        CategorySummaryModel::getCategoryId,
                        java.util.stream.Collectors.reducing(
                                null,
                                (a, b) -> new CategorySummaryModel(b.getCategoryId(), (a != null ? a.getIssues() : 0) + b.getIssues(), (a != null && a.isAcceptable()) || b.isAcceptable())
                        )
                ));

        // Add the Uncategorized category if there are any uncategorized results
        if (uncategorizedCount > 0) {
            groupedCategories.put(UNCATEGORIZED_CATEGORY_ID,
                    new CategorySummaryModel(UNCATEGORIZED_CATEGORY_ID, (int) uncategorizedCount, false));
        }

        return groupedCategories
                .values()
                .stream()
                .toList();
    }

    /**
     * Gets issues associated with a specific category for a facility and report
     *
     * @param facilityId The facility ID
     * @param reportId   The report ID
     * @param categoryId The category ID
     * @return List of CategoryIssueDTO objects
     */
    public List<CategoryIssueModel> getCategoryIssues(String facilityId, String reportId, String categoryId) {
        List<Result> results = resultRepository.findAllByFacilityIdAndReportId(facilityId, reportId);

        // Handle the special case for Uncategorized category
        if (categoryId.equalsIgnoreCase(UNCATEGORIZED_CATEGORY_ID)) {
            return results.stream()
                    .filter(result -> result.getCategories() == null || result.getCategories().isEmpty())
                    .map(result -> new CategoryIssueModel(
                            result.getLocation(),
                            result.getMessage(),
                            result.getCode().toString(),
                            result.getExpression(),
                            result.getPatientId()))
                    .toList();
        }

        // Handle regular categories
        return results.stream()
                .filter(result -> result.getCategories() != null && 
                        result.getCategories().stream().anyMatch(category -> category.getId().equals(categoryId)))
                .map(result -> new CategoryIssueModel(
                        result.getLocation(),
                        result.getMessage(),
                        result.getCode().toString(),
                        result.getExpression(),
                        result.getPatientId()))
                .toList();
    }
}
