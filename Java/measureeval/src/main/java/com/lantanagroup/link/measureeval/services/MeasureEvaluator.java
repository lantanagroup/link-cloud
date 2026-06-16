package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.FhirVersionEnum;
import ca.uhn.fhir.model.api.TemporalPrecisionEnum;
import com.lantanagroup.link.measureeval.models.DebugSections;
import com.lantanagroup.link.measureeval.models.MeasureEvaluationResult;
import com.lantanagroup.link.measureeval.repositories.LinkInMemoryFhirRepository;
import com.lantanagroup.link.measureeval.utils.ParametersUtils;
import com.lantanagroup.link.measureeval.utils.StreamUtils;
import lombok.Getter;
import org.cqframework.cql.cql2elm.LibraryBuilder;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.instance.model.api.IPrimitiveType;
import org.hl7.fhir.r4.model.*;
import ca.uhn.fhir.repository.IRepository;
import org.opencds.cqf.cql.engine.debug.DebugLibraryResultEntry;
import org.opencds.cqf.cql.engine.debug.DebugLocator;
import org.opencds.cqf.cql.engine.debug.DebugResult;
import org.opencds.cqf.cql.engine.debug.DebugResultEntry;
import org.opencds.cqf.cql.engine.execution.EvaluationResult;
import org.opencds.cqf.cql.engine.execution.ExpressionResult;
import org.opencds.cqf.cql.engine.execution.Variable;
import org.opencds.cqf.cql.engine.execution.trace.ExpressionDefTraceFrame;
import org.opencds.cqf.cql.engine.execution.trace.SubExpressionTraceFrame;
import org.opencds.cqf.cql.engine.execution.trace.Trace;
import org.opencds.cqf.cql.engine.execution.trace.TraceFrame;
import org.opencds.cqf.fhir.cql.EvaluationSettings;
import org.opencds.cqf.fhir.cql.engine.retrieve.RetrieveSettings;
import org.opencds.cqf.fhir.cql.engine.terminology.TerminologySettings;
import org.opencds.cqf.fhir.cr.measure.MeasureEvaluationOptions;
import org.opencds.cqf.fhir.cr.measure.common.EvaluationResultFormatter;
import org.opencds.cqf.fhir.cr.measure.common.GroupDef;
import org.opencds.cqf.fhir.cr.measure.common.MeasureDef;
import org.opencds.cqf.fhir.cr.measure.common.MeasurePeriodValidator;
import org.opencds.cqf.fhir.cr.measure.common.PopulationDef;
import org.opencds.cqf.fhir.cr.measure.r4.MeasureDefAndR4MeasureReport;
import org.opencds.cqf.fhir.cr.measure.r4.R4MultiMeasureService;
import org.opencds.cqf.fhir.utility.monad.Eithers;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.time.ZoneOffset;
import java.time.ZonedDateTime;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Date;
import java.util.EnumSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TimeZone;
import java.util.stream.Collectors;

public class MeasureEvaluator {
    private static final Logger logger = LoggerFactory.getLogger(MeasureEvaluator.class);


    public static final String INDIVIDUAL_MEASURE_REPORT_PROFILE =
            "http://hl7.org/fhir/us/davinci-deqm/StructureDefinition/indv-measurereport-deqm";

    private final FhirContext fhirContext;
    private final MeasureEvaluationOptions options;
    @Getter
    private final Bundle bundle;
    private final Measure measure;

    private MeasureEvaluator(FhirContext fhirContext, Bundle bundle) {
        this(fhirContext, bundle, EnumSet.noneOf(DebugSections.class));
    }

    private MeasureEvaluator(FhirContext fhirContext, Bundle bundle, boolean isDebug) {
        this(fhirContext, bundle,
                isDebug ? EnumSet.allOf(DebugSections.class) : EnumSet.noneOf(DebugSections.class));
    }

    private MeasureEvaluator(FhirContext fhirContext, Bundle bundle, Set<DebugSections> debugSections) {
        if (fhirContext.getVersion().getVersion() != FhirVersionEnum.R4) {
            logger.error("Unsupported FHIR version! Expected R4 found {}",
                    fhirContext.getVersion().getVersion().getFhirVersionString());
            throw new IllegalArgumentException("Unsupported FHIR version!");
        }
        this.fhirContext = fhirContext;
        options = MeasureEvaluationOptions.defaultOptions();
        EvaluationSettings evaluationSettings = options.getEvaluationSettings();
        evaluationSettings.getCqlOptions().getCqlCompilerOptions().setSignatureLevel(LibraryBuilder.SignatureLevel.Overloads);
        evaluationSettings.getTerminologySettings()
                .setValuesetPreExpansionMode(TerminologySettings.VALUESET_PRE_EXPANSION_MODE.USE_IF_PRESENT)
                .setValuesetExpansionMode(TerminologySettings.VALUESET_EXPANSION_MODE.PERFORM_NAIVE_EXPANSION)
                .setValuesetMembershipMode(TerminologySettings.VALUESET_MEMBERSHIP_MODE.USE_EXPANSION)
                .setCodeLookupMode(TerminologySettings.CODE_LOOKUP_MODE.USE_CODESYSTEM_URL);
        evaluationSettings.getRetrieveSettings()
                .setTerminologyParameterMode(RetrieveSettings.TERMINOLOGY_FILTER_MODE.FILTER_IN_MEMORY)
                .setSearchParameterMode(RetrieveSettings.SEARCH_FILTER_MODE.FILTER_IN_MEMORY)
                .setProfileMode(RetrieveSettings.PROFILE_MODE.DECLARED);
        evaluationSettings.getCqlOptions().getCqlEngineOptions().setDebugLoggingEnabled(
                DebugSections.needsDebugLogging(debugSections));
        boolean tracingEnabled = DebugSections.needsTracing(debugSections);
        evaluationSettings.getCqlOptions().getCqlEngineOptions().setTracingEnabled(tracingEnabled);
        evaluationSettings.getCqlOptions().getCqlEngineOptions().setDetailedTracingEnabled(tracingEnabled);

        this.bundle = bundle;
        if (!this.bundle.hasEntry()) {
            logger.error("Please provide the necessary artifacts (e.g. Measure and Library resources) in the Bundle entry!");
            throw new IllegalArgumentException("Please provide the necessary artifacts (e.g. Measure and Library resources) in the Bundle entry!");
        }
        try {
            measure = bundle.getEntry().stream()
                    .map(Bundle.BundleEntryComponent::getResource)
                    .filter(Measure.class::isInstance)
                    .map(Measure.class::cast)
                    .reduce(StreamUtils::toOnlyElement)
                    .orElseThrow();
        } catch (Exception e) {
            logger.error("Error encountered during Measure evaluation: {}", e.getMessage());
            throw e;
        }
    }

    public static MeasureEvaluator compile(FhirContext fhirContext, Bundle bundle, boolean isDebug) {
        MeasureEvaluator instance = new MeasureEvaluator(fhirContext, bundle, isDebug);
        instance.compile();
        return instance;
    }

    private void compile() {
        logger.info("Compiling measure: {}", measure.getUrl());
        String subject = "Patient/the-patient";
        Patient patient = new Patient();
        patient.setId(subject);
        Bundle additionalData = new Bundle();
        additionalData.addEntry().setResource(patient);
        doEvaluate(null, null, new StringType(subject), additionalData);
    }

    public static MeasureEvaluationResult compileAndEvaluate(
            FhirContext fhirContext, Bundle bundle, Parameters parameters, Set<DebugSections> debugSections) {
        MeasureEvaluator evaluator = new MeasureEvaluator(fhirContext, bundle, debugSections);
        DateTimeType periodStart = ParametersUtils.getValue(parameters, "periodStart", DateTimeType.class);
        DateTimeType periodEnd = ParametersUtils.getValue(parameters, "periodEnd", DateTimeType.class);
        StringType subject = ParametersUtils.getValue(parameters, "subject", StringType.class);
        Bundle additionalData = ParametersUtils.getResource(parameters, "additionalData", Bundle.class);

        // Fast path: no debug sections requested — skip the MeasureDef capture overhead.
        if (debugSections.isEmpty()) {
            MeasureReport measureReport = evaluator.doEvaluate(periodStart, periodEnd, subject, additionalData);
            return new MeasureEvaluationResult(measureReport, null);
        }

        MeasureDefAndR4MeasureReport defAndReport =
                evaluator.doEvaluateWithDef(periodStart, periodEnd, subject, additionalData);
        MeasureReport measureReport = defAndReport.measureReport();
        addIndividualMeasureReportProfile(measureReport);
        MeasureEvaluationResult.DebugInfo debugInfo = buildDebugInfo(
                defAndReport.measureDef(),
                defAndReport.evaluationResults(),
                debugSections);
        return new MeasureEvaluationResult(measureReport, debugInfo);
    }

    private MeasureDefAndR4MeasureReport doEvaluateWithDef(
            DateTimeType periodStart,
            DateTimeType periodEnd,
            StringType subject,
            Bundle additionalData) {
        IRepository repository = new LinkInMemoryFhirRepository(fhirContext, bundle);
        R4MultiMeasureService measureService = new R4MultiMeasureService(repository, options, null, new MeasurePeriodValidator());
        ZonedDateTime start = periodStart != null && periodStart.getValue() != null
                ? periodStart.getValue().toInstant().atZone(ZoneOffset.UTC) : null;
        ZonedDateTime end = periodEnd != null && periodEnd.getValue() != null
                ? periodEnd.getValue().toInstant().atZone(ZoneOffset.UTC) : null;
        return measureService.evaluateSingleMeasureCaptureDef(
                Eithers.forRight3(measure),
                start,
                end,
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

    private static MeasureEvaluationResult.DebugInfo buildDebugInfo(
            MeasureDef measureDef,
            Map<String, EvaluationResult> evaluationResults,
            Set<DebugSections> sections) {
        MeasureEvaluationResult.DebugInfo debugInfo = new MeasureEvaluationResult.DebugInfo();

        // Groups (includes errors) — derived from MeasureDef, no engine debug flag required.
        if (sections.contains(DebugSections.GROUPS)) {
            debugInfo.setErrors(measureDef.errors() != null ? new ArrayList<>(measureDef.errors()) : List.of());

            List<MeasureEvaluationResult.GroupInfo> groupInfos = new ArrayList<>();
            for (GroupDef group : measureDef.groups()) {
                MeasureEvaluationResult.GroupInfo groupInfo = new MeasureEvaluationResult.GroupInfo();
                groupInfo.setId(group.id());
                groupInfo.setScore(group.getScore());

                List<MeasureEvaluationResult.PopulationInfo> populationInfos = new ArrayList<>();
                for (PopulationDef pop : group.populations()) {
                    MeasureEvaluationResult.PopulationInfo popInfo = new MeasureEvaluationResult.PopulationInfo();
                    popInfo.setType(pop.type().toCode());
                    popInfo.setCount(pop.getCount());
                    popInfo.setSubjects(new ArrayList<>(pop.getSubjects()));
                    populationInfos.add(popInfo);
                }
                groupInfo.setPopulations(populationInfos);
                groupInfos.add(groupInfo);
            }
            debugInfo.setGroups(groupInfos);
        }

        if (evaluationResults == null || evaluationResults.isEmpty()) {
            return debugInfo;
        }

        for (Map.Entry<String, EvaluationResult> evalEntry : evaluationResults.entrySet()) {
            String evalSubjectId = evalEntry.getKey();
            EvaluationResult evalResult = evalEntry.getValue();

            // Expression results — requires debug logging on the engine.
            if (sections.contains(DebugSections.EXPRESSIONS) && evalResult.getExpressionResults() != null) {
                Map<String, String> expressionResults = debugInfo.getExpressionResults();
                if (expressionResults == null) {
                    expressionResults = new LinkedHashMap<>();
                    debugInfo.setExpressionResults(expressionResults);
                }
                for (Map.Entry<String, ExpressionResult> exprEntry : evalResult.getExpressionResults().entrySet()) {
                    expressionResults.put(exprEntry.getKey(), formatValue(exprEntry.getValue().getValue()));
                }
            }

            DebugResult debugResult = evalResult.getDebugResult();
            if (debugResult != null) {
                if (sections.contains(DebugSections.MESSAGES) && debugResult.getMessages() != null) {
                    List<String> cqlMessages = debugInfo.getCqlMessages();
                    if (cqlMessages == null) {
                        cqlMessages = new ArrayList<>();
                        debugInfo.setCqlMessages(cqlMessages);
                    }
                    for (var msg : debugResult.getMessages()) {
                        cqlMessages.add(msg.getMessage());
                    }
                }

                if (sections.contains(DebugSections.LIBRARY_DEBUG) && debugResult.getLibraryResults() != null) {
                    List<MeasureEvaluationResult.CqlLibraryDebug> libraryDebugList = debugInfo.getLibraryDebug();
                    if (libraryDebugList == null) {
                        libraryDebugList = new ArrayList<>();
                        debugInfo.setLibraryDebug(libraryDebugList);
                    }
                    for (Map.Entry<String, DebugLibraryResultEntry> libEntry : debugResult.getLibraryResults().entrySet()) {
                        MeasureEvaluationResult.CqlLibraryDebug libDebug = new MeasureEvaluationResult.CqlLibraryDebug();
                        libDebug.setLibraryName(libEntry.getKey());
                        libDebug.setSubjectId(evalSubjectId);

                        List<MeasureEvaluationResult.CqlNodeDebug> nodes = new ArrayList<>();
                        for (Map.Entry<DebugLocator, List<DebugResultEntry>> locEntry : libEntry.getValue().getResults().entrySet()) {
                            MeasureEvaluationResult.CqlNodeDebug nodeDebug = new MeasureEvaluationResult.CqlNodeDebug();
                            nodeDebug.setLocator(locEntry.getKey().getLocator());
                            nodeDebug.setLocatorType(locEntry.getKey().getLocatorType().name());

                            List<String> resultValues = new ArrayList<>();
                            for (DebugResultEntry dre : locEntry.getValue()) {
                                resultValues.add(formatValue(dre.getValue()));
                            }
                            nodeDebug.setResults(resultValues);
                            nodes.add(nodeDebug);
                        }
                        libDebug.setNodes(nodes);
                        libraryDebugList.add(libDebug);
                    }
                }
            }

            // Traces — requires CqlEngineOptions tracing flags set true.
            if (sections.contains(DebugSections.TRACES)) {
                Trace trace = evalResult.getTrace();
                if (trace != null && trace.getFrames() != null) {
                    List<MeasureEvaluationResult.ExpressionTrace> allTraces = debugInfo.getTraces();
                    if (allTraces == null) {
                        allTraces = new ArrayList<>();
                        debugInfo.setTraces(allTraces);
                    }
                    for (TraceFrame frame : trace.getFrames()) {
                        allTraces.add(buildTraceTree(frame));
                    }
                }
            }

            // Composite human-readable debug log — best with debug logging and tracing both on.
            if (sections.contains(DebugSections.DEBUG_LOG)) {
                StringBuilder debugLog = debugInfo.getDebugLog() != null
                        ? new StringBuilder(debugInfo.getDebugLog())
                        : new StringBuilder();
                if (debugLog.isEmpty()) {
                    debugLog.append("Measure: ").append(measureDef.url())
                            .append(" (").append(measureDef.version()).append(")\n");
                }
                debugLog.append("\n--- Subject: ").append(evalSubjectId).append(" ---\n");
                try {
                    debugLog.append(EvaluationResultFormatter.format(evalResult, 0, true));
                } catch (Exception e) {
                    debugLog.append("(Error formatting evaluation result: ").append(e.getMessage()).append(")\n");
                }
                debugInfo.setDebugLog(debugLog.toString());
            }
        }

        return debugInfo;
    }

    private static MeasureEvaluationResult.ExpressionTrace buildTraceTree(TraceFrame frame) {
        MeasureEvaluationResult.ExpressionTrace trace = new MeasureEvaluationResult.ExpressionTrace();

        if (frame.getLibrary() != null) {
            trace.setLibrary(frame.getLibrary().getId());
            trace.setLibraryVersion(frame.getLibrary().getVersion());
        }

        if (frame.getElement() != null) {
            trace.setLocator(frame.getElement().getLocator());
        }

        if (frame.getContext() != null) {
            trace.setContext(formatValue(frame.getContext().getSecond()));
        }

        if (frame instanceof ExpressionDefTraceFrame exprFrame) {
            if (exprFrame.getElement() != null) {
                trace.setExpressionName(exprFrame.getElement().getName());
            }
            trace.setResult(formatValue(exprFrame.getResult()));

            if (exprFrame.getArguments() != null && !exprFrame.getArguments().isEmpty()) {
                List<MeasureEvaluationResult.VariableInfo> args = new ArrayList<>();
                for (Variable arg : exprFrame.getArguments()) {
                    args.add(new MeasureEvaluationResult.VariableInfo(arg.getName(), formatValue(arg.getValue())));
                }
                trace.setArguments(args);
            }

            if (exprFrame.getSubframes() != null && !exprFrame.getSubframes().isEmpty()) {
                List<MeasureEvaluationResult.ExpressionTrace> children = new ArrayList<>();
                for (TraceFrame child : exprFrame.getSubframes()) {
                    children.add(buildTraceTree(child));
                }
                trace.setChildren(children);
            }
        } else if (frame instanceof SubExpressionTraceFrame subFrame) {
            if (subFrame.getElement() != null) {
                trace.setNodeType(subFrame.getElement().getClass().getSimpleName());
            }
            trace.setResult(formatValue(subFrame.getResult()));

            if (subFrame.getVariables() != null && !subFrame.getVariables().isEmpty()) {
                List<MeasureEvaluationResult.VariableInfo> vars = new ArrayList<>();
                for (Variable v : subFrame.getVariables()) {
                    vars.add(new MeasureEvaluationResult.VariableInfo(v.getName(), formatValue(v.getValue())));
                }
                trace.setVariables(vars);
            }

            if (subFrame.getSubframes() != null && !subFrame.getSubframes().isEmpty()) {
                List<MeasureEvaluationResult.ExpressionTrace> children = new ArrayList<>();
                for (TraceFrame child : subFrame.getSubframes()) {
                    children.add(buildTraceTree(child));
                }
                trace.setChildren(children);
            }
        }

        return trace;
    }

    private static String formatValue(Object value) {
        if (value == null) {
            return "null";
        }
        if (value instanceof IBaseResource resource) {
            return resource.getIdElement().toUnqualifiedVersionless().getValue();
        }
        if (value instanceof Coding coding) {
            StringBuilder sb = new StringBuilder();
            if (coding.hasSystem()) sb.append(coding.getSystem());
            sb.append("|");
            if (coding.hasCode()) sb.append(coding.getCode());
            if (coding.hasDisplay()) sb.append(" \"").append(coding.getDisplay()).append("\"");
            return sb.toString();
        }
        if (value instanceof CodeableConcept cc) {
            if (cc.hasCoding()) {
                String codings = cc.getCoding().stream()
                        .map(MeasureEvaluator::formatValue)
                        .collect(Collectors.joining("; "));
                return "CodeableConcept{" + codings + "}";
            }
            return cc.hasText() ? cc.getText() : "CodeableConcept{}";
        }
        if (value instanceof Reference ref) {
            return ref.hasReference() ? ref.getReference() : String.valueOf(ref);
        }
        if (value instanceof Quantity qty) {
            return qty.getValue() + " " + (qty.hasUnit() ? qty.getUnit() : qty.hasCode() ? qty.getCode() : "");
        }
        if (value instanceof Period period) {
            return (period.hasStart() ? period.getStartElement().getValueAsString() : "?")
                    + " to " + (period.hasEnd() ? period.getEndElement().getValueAsString() : "?");
        }
        if (value instanceof IPrimitiveType<?> primitive) {
            return primitive.getValueAsString();
        }
        if (value instanceof Collection<?> collection) {
            String items = collection.stream()
                    .map(MeasureEvaluator::formatValue)
                    .collect(Collectors.joining(", "));
            return "[" + items + "]";
        }
        return String.valueOf(value);
    }

    private MeasureReport doEvaluate(
            DateTimeType periodStart,
            DateTimeType periodEnd,
            StringType subject,
            Bundle additionalData) {
        IRepository repository = new LinkInMemoryFhirRepository(fhirContext, bundle);
        R4MultiMeasureService measureService = new R4MultiMeasureService(repository, options, null, new MeasurePeriodValidator());
        ZonedDateTime start = periodStart != null && periodStart.getValue() != null
                ? periodStart.getValue().toInstant().atZone(ZoneOffset.UTC) : null;
        ZonedDateTime end = periodEnd != null && periodEnd.getValue() != null
                ? periodEnd.getValue().toInstant().atZone(ZoneOffset.UTC) : null;
        MeasureReport measureReport = measureService.evaluate(
                Eithers.forRight3(measure),
                start,
                end,
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
        addIndividualMeasureReportProfile(measureReport);
        return measureReport;
    }

    /**
     * Ensures the generated MeasureReport declares the DEQM individual MeasureReport profile in
     * Meta.Profile, adding it only if not already present so the profile is never duplicated.
     */
    private static void addIndividualMeasureReportProfile(MeasureReport measureReport) {
        if (measureReport == null) {
            return;
        }
        boolean alreadyPresent = measureReport.getMeta().getProfile().stream()
                .anyMatch(profile -> INDIVIDUAL_MEASURE_REPORT_PROFILE.equals(profile.getValue()));
        if (!alreadyPresent) {
            measureReport.getMeta().addProfile(INDIVIDUAL_MEASURE_REPORT_PROFILE);
        }
    }

    public MeasureReport evaluate(Date periodStart, Date periodEnd, String patientId, Bundle additionalData) {
        TimeZone utc = TimeZone.getTimeZone(ZoneOffset.UTC);
        return evaluate(
                new DateTimeType(periodStart, TemporalPrecisionEnum.MILLI, utc),
                new DateTimeType(periodEnd, TemporalPrecisionEnum.MILLI, utc),
                new StringType(new IdType(ResourceType.Patient.name(), patientId).getValue()),
                additionalData);
    }

    public MeasureReport evaluate(Parameters parameters) {
        DateTimeType periodStart = ParametersUtils.getValue(parameters, "periodStart", DateTimeType.class);
        DateTimeType periodEnd = ParametersUtils.getValue(parameters, "periodEnd", DateTimeType.class);
        StringType subject = ParametersUtils.getValue(parameters, "subject", StringType.class);
        Bundle additionalData = ParametersUtils.getResource(parameters, "additionalData", Bundle.class);
        return evaluate(periodStart, periodEnd, subject, additionalData);
    }

    public MeasureReport evaluate(
            DateTimeType periodStart,
            DateTimeType periodEnd,
            StringType subject,
            Bundle additionalData) {
        List<Bundle.BundleEntryComponent> entries = additionalData.getEntry();

        logger.debug(
                "Evaluating measure: MEASURE=[{}] START=[{}] END=[{}] SUBJECT=[{}] RESOURCES=[{}]",
                measure.getUrl(), periodStart.asStringValue(), periodEnd.asStringValue(), subject, entries.size());

        // Output debug/trace information about the results of the evaluation
        if (logger.isTraceEnabled()) {
            // Output the group/population counts
            for (MeasureReport.MeasureReportGroupComponent group : doEvaluate(periodStart, periodEnd, subject, additionalData).getGroup()) {
                logger.trace("Group {}: {}", group.getId(), group.getPopulation().size());
                for (MeasureReport.MeasureReportGroupPopulationComponent population : group.getPopulation()) {
                    logger.trace("Population {}: {}", population.getCode().getCodingFirstRep().getDisplay(), population.getCount());
                }
            }

            // Output each resource in the bundle
            for (int entryIndex = 0; entryIndex < entries.size(); entryIndex++) {
                Resource resource = entries.get(entryIndex).getResource();
                logger.trace("Resource {}: {}/{}", entryIndex, resource.getResourceType(), resource.getIdPart());
            }
        }
        return doEvaluate(periodStart, periodEnd, subject, additionalData);
    }
}
