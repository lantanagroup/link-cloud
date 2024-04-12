package com.lantanagroup.link.measureeval.controllers;

import com.lantanagroup.link.measureeval.models.UpdateMeasureDefinitionResponse;
import com.lantanagroup.link.measureeval.services.MeasureDefinitionCache;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.server.ResponseStatusException;

@RestController
@RequestMapping("/api/measure-definition")
public class MeasureDefinitionController {
    private final MeasureDefinitionCache measureDefinitionCache;

    public MeasureDefinitionController(MeasureDefinitionCache measureDefinitionCache) {
        this.measureDefinitionCache = measureDefinitionCache;
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
