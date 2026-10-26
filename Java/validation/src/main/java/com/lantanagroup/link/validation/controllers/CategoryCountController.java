package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.models.ReportCategoryCountsModel;
import com.lantanagroup.link.validation.services.CategoryCountService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.server.ResponseStatusException;

@RestController
@RequestMapping("/api/validation/facilities")
@SecurityRequirement(name = "bearer-key")
public class CategoryCountController {
    private final CategoryCountService categoryCountService;

    public CategoryCountController(CategoryCountService categoryCountService) {
        this.categoryCountService = categoryCountService;
    }

    @Operation(
            summary = "Gets a report's stored validation counts per category",
            description = "Counts are written as each patient of the report is validated and are kept after the "
                    + "report's findings are deleted. Each category's title, acceptable and submit flags are as "
                    + "they were when its count was last written, so later changes to a category don't move them. "
                    + "Categories are ordered by issue count, highest first.")
    @ApiResponse(responseCode = "200", description = "The report's counts; the list is empty when no category matches")
    @ApiResponse(responseCode = "404", description = "The report has no stored counts, for example because it was "
            + "validated before counts were stored")
    @GetMapping("/{facilityId}/reports/{reportId}/category-counts")
    public ReportCategoryCountsModel getReportCategoryCounts(
            @PathVariable String facilityId,
            @PathVariable String reportId,
            @Parameter(description = "Only categories with this submit flag; all categories when omitted")
            @RequestParam(name = "submit", required = false) Boolean submit) {
        return categoryCountService.getReportCounts(facilityId, reportId, submit)
                .orElseThrow(() -> new ResponseStatusException(
                        HttpStatus.NOT_FOUND,
                        "Validation counts are unavailable for this report"));
    }
}
