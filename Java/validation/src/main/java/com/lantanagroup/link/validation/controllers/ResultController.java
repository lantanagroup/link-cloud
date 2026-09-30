package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.models.CategoryIssueModel;
import com.lantanagroup.link.validation.models.CategorySummaryModel;
import com.lantanagroup.link.validation.services.ResultService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.springframework.web.bind.annotation.*;

import java.util.List;

@RestController
@RequestMapping("/api/validation/result")
@SecurityRequirement(name = "bearer-key")
public class ResultController {
    private final ResultService resultService;

    public ResultController(ResultService resultService) {
        this.resultService = resultService;
    }

    @Operation(summary = "Gets results for a facility and report; optionally filters by a minimum severity")
    @GetMapping("/{facilityId}/{reportId}")
    public List<Result> getReportResults(
            @PathVariable String facilityId,
            @PathVariable String reportId,
            @RequestParam(name = "severity", defaultValue = "INFORMATION") OperationOutcome.IssueSeverity severity) {
        return resultService.getReportResults(facilityId, reportId, severity);
    }

    @Operation(summary = "Gets results for a facility, report, and patient; optionally filters by a minimum severity")
    @GetMapping("/{facilityId}/{reportId}/{patientId}")
    public List<Result> getReportPatientResults(
            @PathVariable String facilityId,
            @PathVariable String reportId,
            @PathVariable String patientId,
            @RequestParam(name = "severity", defaultValue = "INFORMATION") OperationOutcome.IssueSeverity severity) {
        return resultService.getReportPatientResults(facilityId, reportId, patientId, severity);
    }

    @Operation(summary = "Gets categories that have issues associated with them for a facility and report")
    @GetMapping("/{facilityId}/{reportId}/category")
    public List<CategorySummaryModel> getReportCategories(
            @PathVariable String facilityId,
            @PathVariable String reportId) {
        return resultService.getReportCategories(facilityId, reportId);
    }

    @Operation(summary = "Gets issues associated with a specific category for a facility and report")
    @GetMapping("/{facilityId}/{reportId}/category/{categoryId}/issue")
    public List<CategoryIssueModel> getCategoryIssues(
            @PathVariable String facilityId,
            @PathVariable String reportId,
            @PathVariable String categoryId) {
        return resultService.getCategoryIssues(facilityId, reportId, categoryId);
    }
}
