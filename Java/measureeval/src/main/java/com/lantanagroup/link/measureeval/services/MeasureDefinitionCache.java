package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.stereotype.Service;

@Service
public class MeasureDefinitionCache {
    private final MeasureDefinitionRepository measureDefinitionRepository;

    public MeasureDefinitionCache(MeasureDefinitionRepository measureDefinitionRepository) {
        this.measureDefinitionRepository = measureDefinitionRepository;
    }

    public Bundle getMeasureDefinition(String measureId) {
        return null;
    }

    public void updateMeasureDefinition(Bundle measureDefinition) {

    }

    public void invalidateMeasureDefinition(String measureId) {

    }
}
