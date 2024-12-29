package com.lantanagroup.link.measureeval.controllers;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.annotation.JsonView;
import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.measureeval.serdes.Views;
import com.lantanagroup.link.measureeval.services.MeasureDefinitionBundleValidator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluatorCache;
import com.lantanagroup.link.measureeval.utils.CqlUtils;
import com.lantanagroup.link.shared.auth.PrincipalUser;
import io.opentelemetry.api.trace.Span;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import javassist.NotFoundException;
import org.apache.commons.text.StringEscapeUtils;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Parameters;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.WebDataBinder;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;

@RestController
@RequestMapping("/api/measure-definition")
@PreAuthorize("hasRole('LinkUser')")
public class MeasureDefinitionController {

    private final Logger _logger = LoggerFactory.getLogger(MeasureDefinitionController.class);
    private final MeasureDefinitionRepository repository;
    private final MeasureDefinitionBundleValidator bundleValidator;
    private final MeasureEvaluatorCache evaluatorCache;

    final String[] DISALLOWED_FIELDS = new String[]{};
    @InitBinder
    public void initBinder(WebDataBinder binder) {
        binder.setDisallowedFields(DISALLOWED_FIELDS);
    }

    public MeasureDefinitionController(
            MeasureDefinitionRepository repository,
            MeasureDefinitionBundleValidator bundleValidator,
            MeasureEvaluatorCache evaluatorCache){
        this.repository = repository;
        this.bundleValidator = bundleValidator;
        this.evaluatorCache = evaluatorCache;
    }

    @GetMapping
    @JsonView(Views.Summary.class)
    @Operation(summary = "Get all measure definitions", tags = {"Measure Definitions"})
    public List<MeasureDefinition> getAll(@AuthenticationPrincipal PrincipalUser user) {
        _logger.info("Get all measure definitions");

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }
        return repository.findAll();

    }

    @GetMapping("/{id}")
    @Operation(summary = "Get a measure definition", tags = {"Measure Definitions"})
    public MeasureDefinition getOne(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id) {

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }

        return repository.findById(id).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));
    }

    @PutMapping("/{id}")
    @PreAuthorize("hasAuthority('IsLinkAdmin')")
    @Operation(summary = "Put (create or update) a measure definition", tags = {"Measure Definitions"})
    public MeasureDefinition put(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id, @RequestBody Bundle bundle) {
        _logger.info("Put measure definition {}", StringEscapeUtils.escapeJava(id));

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }
        bundleValidator.validate(bundle);
        MeasureDefinition entity = repository.findById(id).orElseGet(() -> {
            MeasureDefinition _entity = new MeasureDefinition();
            _entity.setId(id);
            return _entity;
        });
        entity.setBundle(bundle);
        repository.save(entity);
        evaluatorCache.remove(id);
        return entity;
    }

    @GetMapping("/{id}/{library-id}/$cql")
    @PreAuthorize("hasAuthority('IsLinkAdmin')")
    @Operation(summary = "Get the CQL for a measure definition's library", tags = {"Measure Definitions"})
    @Parameter(name = "id", description = "The ID of the measure definition", required = true)
    @Parameter(name = "library-id", description = "The ID of the library in the measure definition", required = true)
    @Parameter(name = "range", description = "The range of the CQL to return (e.g. 37:1-38:22)", required = false)
    public String getMeasureLibraryCQL(
            @PathVariable("id") String measureId,
            @PathVariable("library-id") String libraryId,
            @RequestParam(value = "range", required = false) String range) {

        // Test that the range format is correct (i.e. "37:1-38:22")
        if (range != null && !range.matches("\\d+:\\d+-\\d+:\\d+")) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Invalid range format");
        }

        // Get the measure definition from the repo by ID
        MeasureDefinition measureDefinition = repository.findById(measureId).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));

        try {
            return CqlUtils.getCql(measureDefinition.getBundle(), libraryId, range);
        } catch (NotFoundException e) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, e.getMessage(), e);
        }
    }

    @PostMapping("/{id}/$evaluate")
    @PreAuthorize("hasAuthority('IsLinkAdmin')")
    @Operation(summary = "Evaluate a measure against data in request body", tags = {"Measure Definitions"})
    @Parameter(name = "id", description = "The ID of the measure definition", required = true)
    @Parameter(name = "parameters", description = "The parameters to use in the evaluation", required = true)
    @Parameter(name = "debug", description = "Whether to log CQL debugging information during evaluation", required = false)
    public MeasureReport evaluate(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id, @RequestBody Parameters parameters, @RequestParam(required = false, defaultValue = "false") boolean debug) {

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }

        try {
            // Ensure that a measure evaluator is cached (so that CQL logging can use it)
            MeasureEvaluator evaluator = evaluatorCache.get(id);
            // But recompile the bundle every time because the debug flag may not match what's in the cache
            return MeasureEvaluator.compileAndEvaluate(FhirContext.forR4(), evaluator.getBundle(), parameters, debug);
        } catch (Exception e) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, e.getMessage(), e);
        }
    }
}
