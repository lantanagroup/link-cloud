package com.lantanagroup.link.measureeval.controllers;

import com.fasterxml.jackson.annotation.JsonView;
import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.serdes.Views;
import com.lantanagroup.link.measureeval.services.MeasureDefinitionBundleValidator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluatorCache;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Parameters;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;

@RestController
@RequestMapping("/api/measure-definition")
public class MeasureDefinitionController {
    private final MongoOperations mongoOperations;
    private final MeasureDefinitionBundleValidator measureDefinitionBundleValidator;
    private final MeasureEvaluatorCache measureEvaluatorCache;

    public MeasureDefinitionController(
            MongoOperations mongoOperations, MeasureDefinitionBundleValidator measureDefinitionBundleValidator,
            MeasureEvaluatorCache measureEvaluatorCache) {
        this.mongoOperations = mongoOperations;
        this.measureDefinitionBundleValidator = measureDefinitionBundleValidator;
        this.measureEvaluatorCache = measureEvaluatorCache;
    }

    @GetMapping
    @JsonView(Views.Summary.class)
    public List<MeasureDefinition> getAll() {
        return mongoOperations.findAll(MeasureDefinition.class);
    }

    @GetMapping("/{id}")
    public MeasureDefinition getOne(@PathVariable String id) {
        MeasureDefinition entity = mongoOperations.findById(id, MeasureDefinition.class);
        if (entity == null) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND);
        }
        return entity;
    }

    @PutMapping("/{id}")
    public MeasureDefinition put(@PathVariable String id, @RequestBody Bundle bundle) {
        measureDefinitionBundleValidator.validate(bundle);
        MeasureDefinition entity = mongoOperations.findById(id, MeasureDefinition.class);
        if (entity == null) {
            entity = new MeasureDefinition();
            entity.setId(id);
        }
        entity.setBundle(bundle);
        mongoOperations.save(entity);
        measureEvaluatorCache.remove(id);
        return entity;
    }

    @PostMapping("/{id}/$evaluate")
    public MeasureReport evaluate(@PathVariable String id, @RequestBody Parameters parameters) {
        MeasureEvaluator measureEvaluator = measureEvaluatorCache.getOrFind(id);
        if (measureEvaluator == null) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND);
        }
        try {
            return measureEvaluator.evaluate(parameters);
        } catch (Exception e) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, e.getMessage(), e);
        }
    }
}
