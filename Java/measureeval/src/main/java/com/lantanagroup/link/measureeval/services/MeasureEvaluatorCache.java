package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.stereotype.Service;

import java.util.HashMap;
import java.util.Map;

@Service
public class MeasureEvaluatorCache {
    private final FhirContext fhirContext;
    private final MongoOperations mongoOperations;
    private final Map<String, MeasureEvaluator> instancesById = new HashMap<>();

    public MeasureEvaluatorCache(FhirContext fhirContext, MongoOperations mongoOperations) {
        this.fhirContext = fhirContext;
        this.mongoOperations = mongoOperations;
    }

    public MeasureEvaluator get(String id) {
        return instancesById.get(id);
    }

    public MeasureEvaluator getOrFind(String id) {
        return instancesById.computeIfAbsent(id, _id -> {
            MeasureDefinition measureDefinition = mongoOperations.findById(_id, MeasureDefinition.class);
            return measureDefinition == null
                    ? null
                    : MeasureEvaluator.compile(fhirContext, measureDefinition.getBundle());
        });
    }

    public void remove(String id) {
        instancesById.remove(id);
    }
}
