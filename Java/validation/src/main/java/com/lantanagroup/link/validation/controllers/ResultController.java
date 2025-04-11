package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.shared.utils.IssueSeverityUtils;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.springframework.web.bind.annotation.*;

import java.util.List;

@RestController
@RequestMapping("/api/validation/result")
@SecurityRequirement(name = "bearer-key")
public class ResultController {
    private final ResultRepository resultRepository;

    public ResultController(ResultRepository resultRepository) {
        this.resultRepository = resultRepository;
    }

    @Operation(summary = "Gets results for a facility and report; optionally filters by a minimum severity")
    @GetMapping("/{facilityId}/{reportId}")
    public List<Result> getReportResults(
            @PathVariable String facilityId,
            @PathVariable String reportId,
            @RequestParam(name = "severity", defaultValue = "INFORMATION") OperationOutcome.IssueSeverity severity) {
        return resultRepository.findAllByFacilityIdAndReportId(facilityId, reportId).stream()
                .filter(result -> IssueSeverityUtils.isAsSevere(result.getSeverity(), severity))
                .toList();
    }

    @Operation(summary = "Gets results for a facility, report, and patient; optionally filters by a minimum severity")
    @GetMapping("/{facilityId}/{reportId}/{patientId}")
    public List<Result> getReportPatientResults(
            @PathVariable String facilityId,
            @PathVariable String reportId,
            @PathVariable String patientId,
            @RequestParam(name = "severity", defaultValue = "INFORMATION") OperationOutcome.IssueSeverity severity) {
        return resultRepository.findAllByFacilityIdAndReportIdAndPatientId(facilityId, reportId, patientId).stream()
                .filter(result -> IssueSeverityUtils.isAsSevere(result.getSeverity(), severity))
                .toList();
    }
}
