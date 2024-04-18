package com.lantanagroup.link.measureeval.controllers;

import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.measureeval.services.MeasureEvaluationCache;
import org.apache.commons.lang3.StringUtils;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.Measure;
import org.hl7.fhir.r4.model.ResourceType;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.time.Instant;
import java.util.List;

@RestController
@RequestMapping("/api/measure-definition")
public class MeasureDefinitionController {
    private final MeasureEvaluationCache measureEvaluationCache;
    private final MeasureDefinitionRepository measureDefinitionRepository;

    public MeasureDefinitionController(MeasureEvaluationCache measureEvaluationCache, MeasureDefinitionRepository measureDefinitionRepository) {
        this.measureEvaluationCache = measureEvaluationCache;
        this.measureDefinitionRepository = measureDefinitionRepository;
    }

    @GetMapping
    public List<MeasureDefinition> getAll() {
        return this.measureDefinitionRepository.findAll().stream().map(md -> {
            MeasureDefinition measureDefinition = new MeasureDefinition();
            measureDefinition.setId(md.getId());
            measureDefinition.setLastUpdated(md.getLastUpdated());
            return measureDefinition;
        }).toList();
    }

    private void validateBundle(Bundle bundle) {
        if (!bundle.hasId()) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Bundle must have an id");
        }

        Measure foundMeasure = bundle.getEntry().stream()
                .filter(e -> e.getResource() != null && e.getResource().getResourceType() == ResourceType.Measure)
                .map(e -> (Measure) e.getResource())
                .findFirst()
                .orElse(null);

        if (foundMeasure == null) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Bundle must contain a Measure resource");
        }

        if (StringUtils.isEmpty(foundMeasure.getUrl())) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Measure resource must have a url");
        }

        foundMeasure.getLibrary().forEach(libraryReference -> {
            String libraryUrl = libraryReference.asStringValue();

            boolean foundLibrary = bundle.getEntry().stream()
                    .filter(e -> e.getResource() != null && e.getResource().getResourceType() == ResourceType.Library)
                    .map(e -> (Library) e.getResource())
                    .anyMatch(library -> StringUtils.equals(library.getUrl(), libraryUrl));

            if (!foundLibrary) {
                throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Measure resource references a library that is not in the bundle");
            }
        });
    }

    @PutMapping(consumes = {"application/json"})
    public void createOrUpdate(@RequestBody Bundle bundle) {
        this.validateBundle(bundle);

        bundle.setId(bundle.getIdElement().getIdPart().replace("-bundle", ""));

        MeasureDefinition measureDefinition = new MeasureDefinition();
        measureDefinition.setId(bundle.getIdElement().getIdPart());
        measureDefinition.setBundle(bundle);
        measureDefinition.setLastUpdated(Instant.now());
        this.measureDefinitionRepository.save(measureDefinition);

        // Update the cache with the latest measure definition bundle
        this.measureEvaluationCache.update(measureDefinition);
    }
}
