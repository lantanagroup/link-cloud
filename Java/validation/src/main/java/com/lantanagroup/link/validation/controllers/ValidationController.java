package com.lantanagroup.link.validation.controllers;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.shared.utils.IssueSeverityUtils;
import com.lantanagroup.link.shared.utils.LogUtils;
import com.lantanagroup.link.validation.entities.*;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import com.lantanagroup.link.validation.services.CategorizationService;
import com.lantanagroup.link.validation.services.PreQualService;
import com.lantanagroup.link.validation.services.ReportClient;
import com.lantanagroup.link.validation.services.ValidationService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import org.apache.commons.lang3.StringUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.WebDataBinder;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;
import org.springframework.http.ProblemDetail;

import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;
import java.util.stream.Stream;

@RestController
@RequestMapping("/api/validation")
@SecurityRequirement(name = "bearer-key")
public class ValidationController {

    private final ReportClient reportClient;
    private final FhirContext fhirContext;
    private final ValidationService validationService;
    private final CategorizationService categorizationService;
    private final ResultRepository resultRepository;

    private final PreQualService preQualService;

    private final Logger _logger = LoggerFactory.getLogger(ValidationController.class);

    final String[] DISALLOWED_FIELDS = new String[]{};
    @InitBinder
    public void initBinder(WebDataBinder binder) {
        binder.setDisallowedFields(DISALLOWED_FIELDS);
    }

    public ValidationController(
            ReportClient reportClient, FhirContext fhirContext,
            ValidationService validationService,
            CategorizationService categorizationService,
            ResultRepository resultRepository, PreQualService preQualService) {
        this.reportClient = reportClient;
        this.fhirContext = fhirContext;
        this.validationService = validationService;
        this.categorizationService = categorizationService;
        this.resultRepository = resultRepository;
        this.preQualService = preQualService;
    }

    private List<ResultSummary> summarize(List<Result> results, Function<Result, Stream<String>> mapper) {
        return results.stream()
                .flatMap(mapper)
                .collect(Collectors.groupingBy(Function.identity(), Collectors.counting()))
                .entrySet().stream()
                .sorted(Map.Entry.<String, Long>comparingByValue().reversed())
                .map(ResultSummary::new)
                .toList();
    }

    private List<?> getCategorizeResponse(boolean summarize, List<Result> results) {
        for (Result result : results) {
            if (result.getCategories().isEmpty()) {
                result.setCategories(List.of(Category.UNCATEGORIZED));
            }
        }
        return summarize
                ? summarize(results, result -> result.getCategories().stream().map(Category::getTitle))
                : results;
    }

    @Operation(summary = "Validates a FHIR resource")
    @PostMapping("/$validate")
    public List<?> validate(
            @RequestParam(defaultValue = "false") boolean categorize,
            @RequestParam(defaultValue = "false") boolean summarize,
            @RequestBody String json) {
        if (StringUtils.isBlank(json)) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "No resource provided");
        }
        IBaseResource resource;
        try {
            resource = fhirContext.newJsonParser().parseResource(json);
        } catch (Exception e) {
            throw new ResponseStatusException(
                    HttpStatus.BAD_REQUEST,
                    String.format("Failed to parse resource: %s", e.getMessage()));
        }
        List<Result> results = validationService.validate(resource);
        if (categorize) {
            categorizationService.categorize(results);
            return getCategorizeResponse(summarize, results);
        } else {
            return summarize ? summarize(results, result -> Stream.of(result.getMessage())) : results;
        }
    }

    @Operation(summary = "Categorizes validation results using latest rules")
    @PostMapping(path = "/$categorize", consumes = MediaType.APPLICATION_JSON_VALUE)
    public List<?> categorize(
            @RequestParam(defaultValue = "false") boolean summarize,
            @RequestBody List<Result> results) {
        categorizationService.categorize(results);
        return getCategorizeResponse(summarize, results);
    }

    @Operation(summary = "Categorizes validation results using specified rules")
    @PostMapping(path = "/$categorize", consumes = MediaType.MULTIPART_FORM_DATA_VALUE)
    public List<?> categorize(
            @RequestParam(defaultValue = "false") boolean summarize,
            @RequestPart List<Result> results,
            @RequestPart(name = "categories") List<CategorySnapshot> categorySnapshots) {
        categorizationService.categorize(results, categorySnapshots);
        return getCategorizeResponse(summarize, results);
    }

    @Operation(summary = "Download a pre-qual validation report")
    @GetMapping(path = "/pre-qual/{facilityId}/{reportId}",  produces = {"text/html"})
    public ResponseEntity<?> getPreQualValidationReport(
            @PathVariable String facilityId,
            @PathVariable String reportId,
            @RequestParam(name = "severity", defaultValue = "INFORMATION") OperationOutcome.IssueSeverity severity)
    {
        if(facilityId == null || facilityId.isBlank()) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Facility ID is required");
        }

        if(reportId == null || reportId.isBlank()) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Report ID is required");
        }

        ReportScheduleSummaryModel reportSummary;
        try {
            reportSummary = reportClient.getReportScheduleSummaryModel(facilityId, reportId);
        } catch (Exception ex) {
            _logger.error("Unexpected error while retrieving report schedule summary model from report service: {}", ex.getMessage(), ex);
            throw ex;
        }

        //get the results for the specified facility and report
        var results = resultRepository.findAllByFacilityIdAndReportId(facilityId, reportId).stream()
                .filter(result -> IssueSeverityUtils.isAsSevere(result.getSeverity(), severity))
                .toList();

        if (results.isEmpty()) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "No results found for the specified facility and report id");
        }

        // categorize the results
        categorizationService.categorize(results);

        // mark uncategorized results
        for (Result result : results) {
            if (result.getCategories().isEmpty()) {
                result.setCategories(List.of(Category.UNCATEGORIZED));
            }
        }

        // create pre-qual summary
        PreQualSummary summary = new PreQualSummary(reportSummary);
        summary.setResults(results);
        summary.setCategories(results.stream()
                .flatMap(result -> result.getCategories().stream())
                .distinct()
                .toList());

        try {
            String preQualReport = preQualService.generateSimplePreQualReport(summary);

            return ResponseEntity.ok()
                    .contentType(MediaType.TEXT_HTML)
                    .body(preQualReport);
        } catch (Exception e) {

            _logger.error(
                    "Failed to generate pre-qual validation report for facility {} and report {}",
                    LogUtils.sanitize(facilityId),
                    LogUtils.sanitize(reportId),
                    e);
            ProblemDetail problemDetail = ProblemDetail.forStatusAndDetail(
                    HttpStatus.INTERNAL_SERVER_ERROR,
                    "Failed to generate pre-qual validation report"
            );
            problemDetail.setTitle("Internal Server Error");
            problemDetail.setProperty("timestamp", System.currentTimeMillis());

            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(problemDetail);

        }
    }
}
