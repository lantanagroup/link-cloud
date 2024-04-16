package com.lantanagroup.link.measureeval.controllers;

import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.models.UpdateMeasureDefinitionResponse;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.measureeval.services.MeasureDefinitionCache;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;

@RestController
@RequestMapping("/api/measure-definition")
public class MeasureDefinitionController {
    private final MeasureDefinitionCache measureDefinitionCache;
    private final MeasureDefinitionRepository measureDefinitionRepository;

    public MeasureDefinitionController(MeasureDefinitionCache measureDefinitionCache, MeasureDefinitionRepository measureDefinitionRepository) {
        this.measureDefinitionCache = measureDefinitionCache;
        this.measureDefinitionRepository = measureDefinitionRepository;
    }

    @GetMapping
    public List<MeasureDefinition> getAll() {
        return this.measureDefinitionRepository.findAll().stream().map(md -> {
            MeasureDefinition measureDefinition = new MeasureDefinition();
            measureDefinition.setId(md.getId());
            measureDefinition.setMeasureId(md.getMeasureId());
            measureDefinition.setMeasureName(md.getMeasureName());
            measureDefinition.setMeasureDefinitionUrl(md.getMeasureDefinitionUrl());
            measureDefinition.setLastUpdated(md.getLastUpdated());
            return measureDefinition;
        }).toList();
    }

    @PutMapping
    public UpdateMeasureDefinitionResponse createOrUpdate(@RequestBody Bundle bundle) {
        if (!bundle.hasId()) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Bundle must have an id");
        }

        // TODO: Validate the measure definition bundle

        // TODO: Persist the measure definition bundle

        // Update the cache with the latest measure definition bundle
        this.measureDefinitionCache.updateMeasureDefinition(bundle);

        return new UpdateMeasureDefinitionResponse(true);
    }
}
