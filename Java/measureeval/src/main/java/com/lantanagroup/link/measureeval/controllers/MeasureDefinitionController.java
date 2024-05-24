package com.lantanagroup.link.measureeval.controllers;

import com.fasterxml.jackson.annotation.JsonView;
import com.lantanagroup.link.measureeval.auth.PrincipalUser;
import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.measureeval.serdes.Views;
import com.lantanagroup.link.measureeval.services.MeasureDefinitionBundleValidator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluatorCache;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Parameters;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.core.userdetails.UserDetails;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;

@RestController
@RequestMapping("/api/measure-definition")
public class MeasureDefinitionController {
    private final MeasureDefinitionRepository repository;
    private final MeasureDefinitionBundleValidator bundleValidator;
    private final MeasureEvaluatorCache evaluatorCache;

    public MeasureDefinitionController(
            MeasureDefinitionRepository repository,
            MeasureDefinitionBundleValidator bundleValidator,
            MeasureEvaluatorCache evaluatorCache) {
        this.repository = repository;
        this.bundleValidator = bundleValidator;
        this.evaluatorCache = evaluatorCache;
    }

    @GetMapping
    @JsonView(Views.Summary.class)
    @PreAuthorize("hasRole('LinkUser')")
    public List<MeasureDefinition> getAll(@AuthenticationPrincipal UserDetails user) {
        return repository.findAll();
    }

    @GetMapping("/{id}")
    @PreAuthorize("hasRole('LinkUser')")
    public MeasureDefinition getOne(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id) {
        return repository.findById(id).orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND));
    }

    @PutMapping("/{id}")
    @PreAuthorize("hasRole('LinkUser') and hasAuthority('IsLinkAdmin')")
    public MeasureDefinition put(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id, @RequestBody Bundle bundle) {
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
    @PreAuthorize("hasRole('LinkUser') and hasAuthority('IsLinkAdmin')")
    public MeasureReport evaluate(@AuthenticationPrincipal PrincipalUser user, @PathVariable String id, @RequestBody Parameters parameters) {
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
