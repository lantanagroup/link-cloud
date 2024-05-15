package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import org.springframework.stereotype.Service;

import java.util.HashMap;
import java.util.Map;

@Service
public class MeasureEvaluatorCache {
    private final FhirContext fhirContext;
    private final MeasureDefinitionRepository definitionRepository;
    private final Map<String, MeasureEvaluator> instancesById = new HashMap<>();

    public MeasureEvaluatorCache(FhirContext fhirContext, MeasureDefinitionRepository definitionRepository) {
        this.fhirContext = fhirContext;
        this.definitionRepository = definitionRepository;
    }

    public MeasureEvaluator get(String id) {
        return instancesById.computeIfAbsent(id, _id -> {
            MeasureDefinition measureDefinition = definitionRepository.findById(_id).orElse(null);
            if (measureDefinition == null) {
                return null;
            }
            return MeasureEvaluator.compile(fhirContext, measureDefinition.getBundle());
        });
    }

    public void remove(String id) {
        instancesById.remove(id);
    }
}
