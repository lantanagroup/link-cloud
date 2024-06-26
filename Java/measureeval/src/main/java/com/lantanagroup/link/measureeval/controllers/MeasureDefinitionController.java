package com.lantanagroup.link.measureeval.controllers;

import com.fasterxml.jackson.annotation.JsonView;
import com.lantanagroup.link.shared.auth.PrincipalUser;
import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.measureeval.serdes.Views;
import com.lantanagroup.link.measureeval.services.MeasureDefinitionBundleValidator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluatorCache;
import io.opentelemetry.api.trace.Span;
import io.swagger.v3.oas.annotations.Operation;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Parameters;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
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
        _logger.info("Get measure definition {}", id);

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
        _logger.info("Put measure definition {}", id);

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

    @PostMapping("/{id}/$evaluate")
    @PreAuthorize("hasAuthority('IsLinkAdmin')")
    @Operation(summary = "Evaluate a measure against data in request body", tags = {"Measure Definitions"})
    public MeasureReport evaluate(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id, @RequestBody Parameters parameters) {
        _logger.info("Evaluate measure definition {}", id);

        if (user != null){
            Span currentSpan = Span.current();
            currentSpan.setAttribute("user", user.getEmailAddress());
        }
        MeasureEvaluator evaluator = evaluatorCache.get(id);
        if (evaluator == null) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND);
        }
        try {
            return evaluator.evaluate(parameters);
        } catch (Exception e) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, e.getMessage(), e);
        }
    }
}
