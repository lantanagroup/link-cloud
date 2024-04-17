package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import com.lantanagroup.link.measureeval.repositories.MeasureDefinitionRepository;
import com.lantanagroup.link.shared.fhir.FhirHelper;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Measure;
import org.hl7.fhir.r4.model.Patient;
import org.opencds.cqf.fhir.api.Repository;
import org.opencds.cqf.fhir.cql.EvaluationSettings;
import org.opencds.cqf.fhir.cql.engine.retrieve.RetrieveSettings;
import org.opencds.cqf.fhir.cql.engine.terminology.TerminologySettings;
import org.opencds.cqf.fhir.cr.measure.MeasureEvaluationOptions;
import org.opencds.cqf.fhir.cr.measure.r4.R4MeasureService;
import org.opencds.cqf.fhir.utility.monad.Eithers;
import org.opencds.cqf.fhir.utility.repository.InMemoryFhirRepository;
import org.springframework.stereotype.Service;

import java.util.concurrent.ConcurrentHashMap;

@Service
public class MeasureEvaluationCache {
    private final MeasureDefinitionRepository measureDefinitionRepository;
    private ConcurrentHashMap<String, MeasureEvaluationOptions> evalOptions = new ConcurrentHashMap<>();

    public MeasureEvaluationCache(MeasureDefinitionRepository measureDefinitionRepository) {
        this.measureDefinitionRepository = measureDefinitionRepository;
    }

    public MeasureEvaluationOptions getEvaluationOptions(String measureId) {
        if (!this.evalOptions.containsKey(measureId)) {
            MeasureDefinition measureDefinition = this.measureDefinitionRepository.findById(measureId).orElse(null);

            if (measureDefinition == null) {
                throw new RuntimeException("MeasureDefinition not found for measureId: " + measureId);
            }

            return this.update(measureDefinition);
        }

        return this.evalOptions.get(measureId);
    }

    public MeasureEvaluationOptions update(MeasureDefinition measureDefinition) {
        this.invalidate(measureDefinition.getId());

        MeasureEvaluationOptions options = MeasureEvaluationOptions.defaultOptions();
        this.evalOptions.put(measureDefinition.getId(), options);

        EvaluationSettings evaluationSettings = options.getEvaluationSettings();
        evaluationSettings.getTerminologySettings()
                .setValuesetPreExpansionMode(TerminologySettings.VALUESET_PRE_EXPANSION_MODE.USE_IF_PRESENT)
                .setValuesetExpansionMode(TerminologySettings.VALUESET_EXPANSION_MODE.PERFORM_NAIVE_EXPANSION)
                .setValuesetMembershipMode(TerminologySettings.VALUESET_MEMBERSHIP_MODE.USE_EXPANSION)
                .setCodeLookupMode(TerminologySettings.CODE_LOOKUP_MODE.USE_CODESYSTEM_URL);
        evaluationSettings.getRetrieveSettings()
                .setTerminologyParameterMode(RetrieveSettings.TERMINOLOGY_FILTER_MODE.FILTER_IN_MEMORY)
                .setSearchParameterMode(RetrieveSettings.SEARCH_FILTER_MODE.FILTER_IN_MEMORY)
                .setProfileMode(RetrieveSettings.PROFILE_MODE.DECLARED);

        this.preCompile(measureDefinition);

        return options;
    }

    public void invalidate(String measureId) {
        this.evalOptions.remove(measureId);
    }

    private void preCompile(MeasureDefinition measureDefinition) {
        MeasureEvaluationOptions options = this.evalOptions.get(measureDefinition.getId());

        if (options == null) {
            throw new RuntimeException("MeasureEvaluationOptions not found for measureId: " + measureDefinition.getId());
        }

        Measure measure = measureDefinition.getBundle().getEntry().stream()
                .filter(e -> e.getResource() instanceof Measure)
                .map(e -> (Measure) e.getResource())
                .findFirst()
                .orElse(null);

        Repository repository = new InMemoryFhirRepository(FhirHelper.getContext());
        measureDefinition.getBundle().getEntry().forEach(e -> repository.update(e.getResource()));
        R4MeasureService measureService = new R4MeasureService(repository, options);

        String subject = "Patient/the-patient";
        Bundle dummyPatientBundle = new Bundle();
        Patient dummyPatient = new Patient();
        dummyPatient.setId(subject);
        dummyPatientBundle.addEntry().setResource(dummyPatient);

        measureService.evaluate(
                Eithers.forRight3(measure),
                null,
                null,
                null,
                subject,
                null,
                null,
                null,
                null,
                dummyPatientBundle,
                null,
                null,
                null);
    }
}
