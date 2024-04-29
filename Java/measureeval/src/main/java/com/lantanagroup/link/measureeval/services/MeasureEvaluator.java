package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.utils.ParametersUtils;
import com.lantanagroup.link.measureeval.utils.StreamUtils;
import org.hl7.fhir.r4.model.*;
import org.opencds.cqf.fhir.api.Repository;
import org.opencds.cqf.fhir.cql.EvaluationSettings;
import org.opencds.cqf.fhir.cql.engine.retrieve.RetrieveSettings;
import org.opencds.cqf.fhir.cql.engine.terminology.TerminologySettings;
import org.opencds.cqf.fhir.cr.measure.MeasureEvaluationOptions;
import org.opencds.cqf.fhir.cr.measure.r4.R4MeasureService;
import org.opencds.cqf.fhir.utility.monad.Eithers;
import org.opencds.cqf.fhir.utility.repository.InMemoryFhirRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.List;

public class MeasureEvaluator {
    private static final Logger logger = LoggerFactory.getLogger(MeasureEvaluator.class);

    private final FhirContext fhirContext;
    private final MeasureEvaluationOptions options;
    private final Bundle bundle;
    private final Measure measure;

    private MeasureEvaluator(FhirContext fhirContext, Bundle bundle) {
        this.fhirContext = fhirContext;
        options = MeasureEvaluationOptions.defaultOptions();
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
        this.bundle = bundle;
        measure = bundle.getEntry().stream()
                .map(Bundle.BundleEntryComponent::getResource)
                .filter(Measure.class::isInstance)
                .map(Measure.class::cast)
                .reduce(StreamUtils::toOnlyElement)
                .orElseThrow();
    }

    public static MeasureEvaluator compile(FhirContext fhirContext, Bundle bundle) {
        MeasureEvaluator instance = new MeasureEvaluator(fhirContext, bundle);
        instance.compile();
        return instance;
    }

    private void compile() {
        logger.debug("Compiling measure: {}", measure.getUrl());
        String subject = "Patient/the-patient";
        Patient patient = new Patient();
        patient.setId(subject);
        Bundle additionalData = new Bundle();
        additionalData.addEntry().setResource(patient);
        doEvaluate(null, null, new StringType(subject), additionalData);
    }

    private MeasureReport doEvaluate(
            DateTimeType periodStart,
            DateTimeType periodEnd,
            StringType subject,
            Bundle additionalData) {
        Repository repository = new InMemoryFhirRepository(fhirContext, bundle);
        R4MeasureService measureService = new R4MeasureService(repository, options);
        return measureService.evaluate(
                Eithers.forRight3(measure),
                periodStart == null ? null : periodStart.asStringValue(),
                periodEnd == null ? null : periodEnd.asStringValue(),
                null,
                subject.asStringValue(),
                null,
                null,
                null,
                null,
                additionalData,
                null,
                null,
                null);
    }

    public MeasureReport evaluate(
            DateTimeType periodStart,
            DateTimeType periodEnd,
            StringType subject,
            Bundle additionalData) {
        List<Bundle.BundleEntryComponent> entries = additionalData.getEntry();
        logger.debug(
                "Evaluating {} from {} to {} with {} additional resources",
                subject, periodStart, periodEnd, entries.size());
        if (logger.isTraceEnabled()) {
            for (int entryIndex = 0; entryIndex < entries.size(); entryIndex++) {
                Resource resource = entries.get(entryIndex).getResource();
                logger.trace("Resource {}: {}", entryIndex, resource.getId());
            }
        }
        return doEvaluate(periodStart, periodEnd, subject, additionalData);
    }

    public MeasureReport evaluate(Parameters parameters) {
        DateTimeType periodStart = ParametersUtils.getValue(parameters, "periodStart", DateTimeType.class);
        DateTimeType periodEnd = ParametersUtils.getValue(parameters, "periodEnd", DateTimeType.class);
        StringType subject = ParametersUtils.getValue(parameters, "subject", StringType.class);
        Bundle additionalData = ParametersUtils.getResource(parameters, "additionalData", Bundle.class);
        return evaluate(periodStart, periodEnd, subject, additionalData);
    }
}
