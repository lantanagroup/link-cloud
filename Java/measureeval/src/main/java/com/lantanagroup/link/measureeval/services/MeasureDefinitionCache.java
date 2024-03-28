package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.Bundle;
import org.springframework.stereotype.Service;

@Service
public class MeasureDefinitionCache {
    public Bundle getMeasureDefinition(String measureId) {
        return null;
    }

    public void updateMeasureDefinition(Bundle measureDefinition) {

    }

    public void invalidateMeasureDefinition(String measureId) {

    }
}
