package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.configs.LinkConfig;
import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.measureeval.utils.CqlUtils;
import org.hl7.fhir.r4.model.Library;
import org.springframework.stereotype.Service;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

@Service
public class MeasureEvaluatorCache implements LibraryResolver {
    private final FhirContext fhirContext;
    private final MeasureDefinitionRepository definitionRepository;
    private final Map<String, MeasureEvaluator> instancesById = new ConcurrentHashMap<>();
    private final LinkConfig linkConfig;

    public MeasureEvaluatorCache(FhirContext fhirContext, MeasureDefinitionRepository definitionRepository, LinkConfig linkConfig) {
        this.fhirContext = fhirContext;
        this.definitionRepository = definitionRepository;
        this.linkConfig = linkConfig;
    }

    public MeasureEvaluator get(String id) {
        return instancesById.computeIfAbsent(id, _id -> {
            MeasureDefinition measureDefinition = definitionRepository.findById(_id).orElse(null);
            if (measureDefinition == null) {
                return null;
            }
            return MeasureEvaluator.compile(fhirContext, measureDefinition.getBundle(), this.linkConfig.isCqlDebug());
        });
    }

    public void remove(String id) {
        instancesById.remove(id);
    }

    @Override
    public Library resolve(String libraryId) {
        for (MeasureEvaluator instance : instancesById.values()) {
            Library library = CqlUtils.getLibrary(instance.getBundle(), libraryId);
            if (library != null) {
                return library;
            }
        }
        return null;
    }
}
