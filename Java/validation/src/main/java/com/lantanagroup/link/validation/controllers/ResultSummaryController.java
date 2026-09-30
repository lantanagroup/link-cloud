package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.models.ResultSummaryModel;
import com.lantanagroup.link.validation.services.ResultService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.server.ResponseStatusException;

@RestController
@RequestMapping("/api/validation/result-summaries")
@SecurityRequirement(name = "bearer-key")
public class ResultSummaryController {
    private final ResultService resultService;

    public ResultSummaryController(ResultService resultService) {
        this.resultService = resultService;
    }

    @Operation(summary = "Counts results for a facility and report at or above a minimum severity, without result bodies")
    @GetMapping("/{facilityId}/{reportId}")
    public ResultSummaryModel summarizeReportResults(
            @PathVariable String facilityId,
            @PathVariable String reportId,
            @RequestParam(name = "severity", defaultValue = "INFORMATION") OperationOutcome.IssueSeverity severity) {
        if (severity == null || severity == OperationOutcome.IssueSeverity.NULL) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Invalid severity");
        }
        return resultService.summarizeReportResults(facilityId, reportId, severity);
    }
}
