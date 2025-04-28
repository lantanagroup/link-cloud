package com.lantanagroup.link.validation.controllers;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.shared.Timer;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategorySnapshot;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ResultSummary;
import com.lantanagroup.link.validation.services.CategorizationService;
import com.lantanagroup.link.validation.services.ValidationService;
import io.opentelemetry.api.common.Attributes;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import org.apache.commons.lang3.StringUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import io.opentelemetry.api.metrics.LongCounter;
import io.opentelemetry.api.metrics.Meter;
import io.opentelemetry.api.metrics.DoubleHistogram;

import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;
import java.util.stream.Stream;

@RestController
@RequestMapping("/api/validation")
@SecurityRequirement(name = "bearer-key")
public class ValidationController {
    private static final Logger logger = LoggerFactory.getLogger(ValidationController.class);
    private final FhirContext fhirContext;
    private final ValidationService validationService;
    private final CategorizationService categorizationService;
    private final LongCounter validationResultsCounter;
    private final DoubleHistogram validationDurationHistogram;

    public ValidationController(
            FhirContext fhirContext,
            ValidationService validationService,
            CategorizationService categorizationService,
            Meter meter) {
        this.fhirContext = fhirContext;
        this.validationService = validationService;
        this.categorizationService = categorizationService;
        this.validationResultsCounter = meter.counterBuilder("validation.results.count")
                .setDescription("Number of validation results")
                .build();
        this.validationDurationHistogram = meter.histogramBuilder("validation.duration")
                .setDescription("Time taken to validate resource")
                .setUnit("s")
                .build();
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

        List<Result> results;
        
        try (Timer timer = Timer.start()) {
            results = validationService.validate(resource);

            validationResultsCounter.add(results.size());
            validationDurationHistogram.record(timer.getSeconds());
            logger.info("Validation completed with {} results in {} seconds", results.size(), String.format("%.2f", timer.getSeconds()));
        }

        if (categorize) {
            try (Timer timer = Timer.start()) {
                categorizationService.categorize(results);
                validationDurationHistogram.record(timer.getSeconds());
                logger.info("Categorization completed in {} seconds", String.format("%.2f", timer.getSeconds()));
            }

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
}
