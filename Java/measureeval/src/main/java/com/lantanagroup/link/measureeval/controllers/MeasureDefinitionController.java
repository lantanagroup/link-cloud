package com.lantanagroup.link.measureeval.controllers;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.annotation.JsonView;
import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.models.DebugSections;
import com.lantanagroup.link.measureeval.models.MeasureEvaluationResult;
import com.lantanagroup.link.measureeval.models.RelatedArtifactInfo;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.measureeval.services.MeasureDefinitionBundleValidator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluatorCache;
import com.lantanagroup.link.measureeval.services.MeasureValidationService;
import com.lantanagroup.link.measureeval.utils.CqlUtils;
import com.lantanagroup.link.shared.auth.PrincipalUser;
import com.lantanagroup.link.shared.serdes.Views;
import io.opentelemetry.api.trace.Span;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import org.apache.commons.text.StringEscapeUtils;
import org.hl7.fhir.r4.model.Bundle;
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
import java.util.Set;

@RestController
@RequestMapping("/api/measureeval/measure-definition")
@PreAuthorize("hasAnyRole('LinkUser', 'LinkAdministrator')")
public class MeasureDefinitionController {

    private final Logger _logger = LoggerFactory.getLogger(MeasureDefinitionController.class);
    private final MeasureDefinitionRepository repository;
    private final MeasureDefinitionBundleValidator bundleValidator;
    private final MeasureEvaluatorCache evaluatorCache;
    private final MeasureValidationService validationService;

    final String[] DISALLOWED_FIELDS = new String[]{};
    @InitBinder
    public void initBinder(WebDataBinder binder) {
        binder.setDisallowedFields(DISALLOWED_FIELDS);
    }

    public MeasureDefinitionController(
            MeasureDefinitionRepository repository,
            MeasureDefinitionBundleValidator bundleValidator,
            MeasureEvaluatorCache evaluatorCache,
            MeasureValidationService validationService){
        this.repository = repository;
        this.bundleValidator = bundleValidator;
        this.evaluatorCache = evaluatorCache;
        this.validationService = validationService;
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
    @JsonView(Views.Detail.class)
    @Operation(summary = "Get a measure definition", tags = {"Measure Definitions"})
    public MeasureDefinition getOne(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id) {

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }

        return repository.findById(id).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));
    }

    @PutMapping
    @PreAuthorize("hasAuthority('IsLinkAdmin')")
    @Operation(summary = "Put (create or update) a measure definition", tags = {"Measure Definitions"})
    @JsonView(Views.Detail.class)
    public MeasureDefinition put(@AuthenticationPrincipal PrincipalUser user, @RequestBody Bundle bundle) {

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }
        if (bundle == null) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Request body must be a FHIR Bundle");
        }
        String id = (bundle.getIdElement() != null) ? bundle.getIdElement().getIdPart() : null;
        if (id == null || id.isBlank()) {
           throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Bundle.id is required");
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
        } catch (CqlUtils.ResourceNotFoundException e) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, e.getMessage(), e);
        }
    }

    @PostMapping("/{id}/$evaluate")
    @PreAuthorize("hasAuthority('IsLinkAdmin')")
    @Operation(summary = "Evaluate a measure against data in request body", tags = {"Measure Definitions"})
    @Parameter(name = "id", description = "The ID of the measure definition", required = true)
    @Parameter(name = "parameters", description = "The parameters to use in the evaluation", required = true)
    @Parameter(name = "debug",
            description = "Which debug sections to populate on the response. " +
                    "Accepts `true`/`all` (every section), `false`/empty (no sections), " +
                    "or a comma-separated list of: groups, expressions, librarydebug, messages, traces, debuglog.",
            required = false)
    public Object evaluate(
            @AuthenticationPrincipal PrincipalUser user,
            @PathVariable String id,
            @RequestBody Parameters parameters,
            @RequestParam(required = false, defaultValue = "false") Set<DebugSections> debug) {

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }

        // Resolve the cached evaluator. Two things this gives us:
        //   1. A cheap existence check for the measure (returns null -> 404).
        //   2. Side effect: ensures the measure's libraries are registered with
        //      MeasureEvaluatorCache's LibraryResolver, which CqlLogAppender uses
        //      to translate CQL log locators back to source ranges.
        // We intentionally do NOT call evaluator.evaluate(...) here. The cached
        // evaluator was compiled once at first-use time with `linkConfig.cqlDebug`
        // baked in as its only debug flag; reusing it would either log too much or
        // too little relative to the per-request `debug` parameter. So we always
        // recompile via compileAndEvaluate(...) with the explicit per-request
        // section set, which sets the engine's debug/tracing flags correctly for
        // this specific call.
        MeasureEvaluator evaluator = evaluatorCache.get(id);

        if (evaluator == null) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "Measure definition not found");
        }

        if (!debug.isEmpty()) {
            _logger.info("Measure evaluation requested with debug sections {} for measure {}",
                    StringEscapeUtils.escapeJava(String.valueOf(debug)),
                    StringEscapeUtils.escapeJava(id));
        }

        try {
            MeasureEvaluationResult result = MeasureEvaluator.compileAndEvaluate(
                    FhirContext.forR4(), evaluator.getBundle(), parameters, debug);
            // Preserve the original wire contract: when no debug sections are
            // requested, return a bare MeasureReport. Only emit the wrapper for
            // callers who opted in via the `debug` query parameter.
            return debug.isEmpty() ? result.getMeasureReport() : result;
        } catch (Exception e) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, e.getMessage(), e);
        }
    }

    @GetMapping("/{id}/relatedArtifact")
    @Operation(summary = "Get related artifacts for a measure definition", tags = {"Measure Definitions"})
    @Parameter(name = "id", description = "The ID of the measure definition", required = true)
    public List<RelatedArtifactInfo> getRelatedArtifacts(@PathVariable String id) {
        return validationService.getRelatedArtifacts(id);
    }
}
