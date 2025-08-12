package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.models.CategoryIssueModel;
import com.lantanagroup.link.validation.models.CategorySummaryModel;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.Arrays;
import java.util.Collections;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class ResultServiceTest {

    @Mock
    private ResultRepository resultRepository;

    private ResultService resultService;

    private static final String FACILITY_ID = "facility1";
    private static final String REPORT_ID = "report1";
    private static final String PATIENT_ID = "patient1";
    private static final String CATEGORY_ID1 = "category1";
    private static final String CATEGORY_ID2 = "category2";

    @BeforeEach
    void setUp() {
        resultService = new ResultService(resultRepository);
    }

    @Test
    void getReportResults_shouldFilterBySeverity() {
        // Arrange
        Result result1 = createResult(OperationOutcome.IssueSeverity.ERROR);
        Result result2 = createResult(OperationOutcome.IssueSeverity.WARNING);
        Result result3 = createResult(OperationOutcome.IssueSeverity.INFORMATION);

        when(resultRepository.findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID))
                .thenReturn(Arrays.asList(result1, result2, result3));

        // Act
        List<Result> results = resultService.getReportResults(FACILITY_ID, REPORT_ID, OperationOutcome.IssueSeverity.WARNING);

        // Assert
        assertEquals(2, results.size());
        assertTrue(results.contains(result1)); // ERROR is more severe than WARNING
        assertTrue(results.contains(result2)); // WARNING matches the threshold
        assertFalse(results.contains(result3)); // INFORMATION is less severe than WARNING

        verify(resultRepository).findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID);
    }

    @Test
    void getReportPatientResults_shouldFilterBySeverity() {
        // Arrange
        Result result1 = createResult(OperationOutcome.IssueSeverity.ERROR);
        Result result2 = createResult(OperationOutcome.IssueSeverity.WARNING);
        Result result3 = createResult(OperationOutcome.IssueSeverity.INFORMATION);

        when(resultRepository.findAllByFacilityIdAndReportIdAndPatientId(FACILITY_ID, REPORT_ID, PATIENT_ID))
                .thenReturn(Arrays.asList(result1, result2, result3));

        // Act
        List<Result> results = resultService.getReportPatientResults(FACILITY_ID, REPORT_ID, PATIENT_ID, 
                OperationOutcome.IssueSeverity.WARNING);

        // Assert
        assertEquals(2, results.size());
        assertTrue(results.contains(result1)); // ERROR is more severe than WARNING
        assertTrue(results.contains(result2)); // WARNING matches the threshold
        assertFalse(results.contains(result3)); // INFORMATION is less severe than WARNING

        verify(resultRepository).findAllByFacilityIdAndReportIdAndPatientId(FACILITY_ID, REPORT_ID, PATIENT_ID);
    }

    @Test
    void getReportCategories_shouldGroupAndCountIssues() {
        // Arrange
        // Create results with categories
        Result result1 = createResultWithCategory(CATEGORY_ID1, true);
        Result result2 = createResultWithCategory(CATEGORY_ID1, false);
        Result result3 = createResultWithCategory(CATEGORY_ID2, true);
        Result resultNoCategory = createResult(OperationOutcome.IssueSeverity.ERROR);

        when(resultRepository.findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID))
                .thenReturn(Arrays.asList(result1, result2, result3, resultNoCategory));

        // Act
        List<CategorySummaryModel> categories = resultService.getReportCategories(FACILITY_ID, REPORT_ID);

        // Assert
        assertEquals(3, categories.size()); // Now 3 categories including Uncategorized

        // Find category1 in the results
        CategorySummaryModel category1 = categories.stream()
                .filter(c -> c.getCategoryId().equals(CATEGORY_ID1))
                .findFirst()
                .orElse(null);

        assertNotNull(category1);
        assertEquals(2, category1.getIssues()); // 2 issues for category1
        assertTrue(category1.isAcceptable()); // One of the results for category1 is acceptable

        // Verify the Uncategorized category
        CategorySummaryModel uncategorizedCategory = categories.stream()
                .filter(c -> c.getCategoryId().equals("uncategorized"))
                .findFirst()
                .orElse(null);

        assertNotNull(uncategorizedCategory);
        assertEquals(1, uncategorizedCategory.getIssues()); // 1 uncategorized issue
        assertFalse(uncategorizedCategory.isAcceptable()); // Uncategorized issues are not acceptable

        verify(resultRepository).findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID);
    }

    @Test
    void getCategoryIssues_shouldReturnOnlyMatchingCategoryIssues() {
        // Arrange
        Result result1 = createResultWithCategory(CATEGORY_ID1, true);
        Result result2 = createResultWithCategory(CATEGORY_ID1, false);
        Result result3 = createResultWithCategory(CATEGORY_ID2, true);
        Result resultNoCategory = createResult(OperationOutcome.IssueSeverity.ERROR);

        when(resultRepository.findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID))
                .thenReturn(Arrays.asList(result1, result2, result3, resultNoCategory));

        // Act
        List<CategoryIssueModel> issues = resultService.getCategoryIssues(FACILITY_ID, REPORT_ID, CATEGORY_ID1);

        // Assert
        assertEquals(2, issues.size());

        // Check that all issues have the expected data
        for (CategoryIssueModel issue : issues) {
            assertEquals("location", issue.getLocation());
            assertEquals("message", issue.getMessage());
            assertEquals("NULL", issue.getCode());
            assertEquals("expression", issue.getExpression());
            assertEquals(PATIENT_ID, issue.getPatientId());
        }

        verify(resultRepository).findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID);
    }

    @Test
    void getCategoryIssues_shouldReturnUncategorizedIssues() {
        // Arrange
        Result result1 = createResultWithCategory(CATEGORY_ID1, true);
        Result result2 = createResultWithCategory(CATEGORY_ID1, false);
        Result resultNoCategory1 = createResult(OperationOutcome.IssueSeverity.ERROR);
        Result resultNoCategory2 = createResult(OperationOutcome.IssueSeverity.WARNING);

        when(resultRepository.findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID))
                .thenReturn(Arrays.asList(result1, result2, resultNoCategory1, resultNoCategory2));

        // Act
        List<CategoryIssueModel> issues = resultService.getCategoryIssues(FACILITY_ID, REPORT_ID, "uncategorized");

        // Assert
        assertEquals(2, issues.size());

        // Check that all issues have the expected data and are indeed the uncategorized ones
        for (CategoryIssueModel issue : issues) {
            assertEquals("location", issue.getLocation());
            assertEquals("message", issue.getMessage());
            assertEquals("NULL", issue.getCode());
            assertEquals("expression", issue.getExpression());
            assertEquals(PATIENT_ID, issue.getPatientId());
        }

        verify(resultRepository).findAllByFacilityIdAndReportId(FACILITY_ID, REPORT_ID);
    }

    // Helper methods to create test data
    private Result createResult(OperationOutcome.IssueSeverity severity) {
        Result result = new Result();
        result.setSeverity(severity);
        result.setCode(OperationOutcome.IssueType.NULL);
        result.setMessage("message");
        result.setLocation("location");
        result.setExpression("expression");
        result.setPatientId(PATIENT_ID);
        result.setFacilityId(FACILITY_ID);
        result.setReportId(REPORT_ID);
        return result;
    }

    private Result createResultWithCategory(String categoryId, boolean acceptable) {
        Result result = createResult(OperationOutcome.IssueSeverity.ERROR);

        Category category = new Category();
        category.setId(categoryId);
        category.setAcceptable(acceptable);

        result.setCategories(Collections.singletonList(category));
        return result;
    }
}
