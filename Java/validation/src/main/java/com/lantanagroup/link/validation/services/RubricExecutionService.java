package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import com.lantanagroup.link.validation.models.EvaluateRequestDto;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.models.SubjectDto;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import com.lantanagroup.link.validation.services.categoryoverride.CategoryOverrideEngine;
import com.lantanagroup.link.validation.services.execution.BundleReferenceResolver;
import com.lantanagroup.link.validation.services.execution.CheckExecutionResult;
import com.lantanagroup.link.validation.services.execution.CheckExecutorRegistry;
import com.lantanagroup.link.validation.services.execution.CheckOutcome;
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.shared.utils.LogUtils;
import lombok.extern.slf4j.Slf4j;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.time.OffsetDateTime;
import java.util.*;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executor;
import java.util.stream.Collectors;

@Service
@Slf4j
public class RubricExecutionService {

    private final RubricVersionResolver versionResolver;
    private final RubricVersionRepository rubricVersionRepository;
    private final CheckExecutorRegistry executorRegistry;
    private final ResultEnvelopeAssembler envelopeAssembler;
    private final CategoryOverrideEngine categoryOverrideEngine;
    private final ScoreAggregator scoreAggregator;
    private final RubricResultPersister resultPersister;
    private final FhirContext fhirContext;
    private final ObjectMapper objectMapper;
    private final Executor checkExecutorPool;
    private final BundleReferenceResolver referenceResolver;

    private final boolean parallel;

    public RubricExecutionService(RubricVersionResolver versionResolver,
                                  RubricVersionRepository rubricVersionRepository,
                                  CheckExecutorRegistry executorRegistry,
                                  ResultEnvelopeAssembler envelopeAssembler,
                                  CategoryOverrideEngine categoryOverrideEngine,
                                  ScoreAggregator scoreAggregator,
                                  RubricResultPersister resultPersister,
                                  FhirContext fhirContext,
                                  ObjectMapper objectMapper,
                                  @Qualifier("checkExecutorPool") Executor checkExecutorPool,
                                  BundleReferenceResolver referenceResolver,
                                  @Value("${vaas.checks.parallel:false}") boolean parallel) {
        this.versionResolver = versionResolver;
        this.rubricVersionRepository = rubricVersionRepository;
        this.executorRegistry = executorRegistry;
        this.envelopeAssembler = envelopeAssembler;
        this.categoryOverrideEngine = categoryOverrideEngine;
        this.scoreAggregator = scoreAggregator;
        this.resultPersister = resultPersister;
        this.fhirContext = fhirContext;
        this.objectMapper = objectMapper;
        this.checkExecutorPool = checkExecutorPool;
        this.referenceResolver = referenceResolver;
        this.parallel = parallel;
        log.info("Rubric check execution mode: {} (vaas.checks.parallel={})",
                parallel ? "parallel fan-out" : "sequential", parallel);
    }


    public ValidationResultEnvelope evaluate(
            String rubricId, String semver, EvaluateRequestDto request, boolean persist, String correlationId) {
        OffsetDateTime requestedAt = OffsetDateTime.now();


        // the resolver returns version + checks together (soft-deleted checks already excluded)
        RubricVersionResolver.ResolvedRubric resolved = versionResolver.resolve(rubricId, semver, persist);
        RubricVersion version = resolved.version();
        List<RubricCheck> checks = resolved.checks();
        log.debug("Resolved rubric {} v{} ({})", version.getRubricId(), version.getSemver(), version.getRubricVersionId());

        SubjectDto subject = request.getSubject();

        Map<String, Object> contextVars = new HashMap<>();
        if (request.getContext() != null) contextVars.putAll(request.getContext());

        IBaseResource resource = parseResource(request.getPayload());

        List<IBaseResource> bundleEntries = Collections.emptyList();
        if (resource instanceof Bundle bundle) {
            bundleEntries = bundle.getEntry().stream()
                    .filter(e -> e.getResource() != null)
                    .map(Bundle.BundleEntryComponent::getResource)
                    .collect(Collectors.toList());
            log.debug("Bundle payload — extracted {} entries", bundleEntries.size());
        }

        ExecutionContext ctx = ExecutionContext.builder()
                .requestId(UUID.randomUUID())
                .correlationId(correlationId)
                .subject(subject)
                .resource(resource)
                .bundleEntries(bundleEntries)
                .referenceIndex(BundleReferenceResolver.buildIndex(bundleEntries))
                .rawPayload(request.getPayload())
                .contextVars(contextVars)
                .requestedAt(requestedAt)
                .build();

        List<RubricCheck> enabled = checks.stream()
                .filter(RubricCheck::isEnabled)
                .collect(Collectors.toList());

        List<CheckOutcome> outcomes = parallel
                ? runParallel(enabled, ctx, resource)
                : runSequential(enabled, ctx, resource);


        List<RawFinding> allFindings = new ArrayList<>();
        List<CheckSlice> slices = new ArrayList<>(outcomes.size());
        Map<String, Long> checkDurations = new LinkedHashMap<>();
        for (CheckOutcome outcome : outcomes) {
            int from = allFindings.size();
            allFindings.addAll(outcome.findings());
            slices.add(new CheckSlice(outcome.checkLocalId(), from, allFindings.size()));
            checkDurations.put(outcome.checkLocalId(), outcome.durationMs());
        }

        Map<String, RubricCheck> checkByLocalId = enabled.stream()
                .collect(Collectors.toMap(RubricCheck::getCheckLocalId, c -> c, (a, b) -> a, LinkedHashMap::new));

        List<EvaluatedFinding> evaluated = categoryOverrideEngine.apply(allFindings);
        if (evaluated.size() != allFindings.size()) {
            throw new IllegalStateException(String.format(
                    "Category override returned %d finding(s) for %d input(s); slices would be misaligned",
                    evaluated.size(), allFindings.size()));
        }

        // per-check results (every check, pass or fail) so check-based scoring policies can score
        List<CheckExecutionResult> checkResults = new ArrayList<>(slices.size());
        for (CheckSlice slice : slices) {
            RubricCheck c = checkByLocalId.get(slice.checkLocalId());
            checkResults.add(CheckExecutionResult.builder()
                    .checkLocalId(slice.checkLocalId())
                    .dimension(c != null ? c.getDimension() : null)
                    .status(scoreAggregator.collapseCheckStatus(slice.findingsIn(evaluated)))
                    .build());
        }

        OffsetDateTime completedAt = OffsetDateTime.now();
        ResultEnvelopeAssembler.AssembleOutput out =
                envelopeAssembler.assemble(ctx, version, evaluated, checkResults, checkDurations, completedAt);

        if (persist) {
            resultPersister.persist(out.resultEntity(), out.findingEntities());
        } else {
            rubricVersionRepository.recordDryRun(
                    version.getRubricVersionId(), out.resultEntity().getStatus(), completedAt);
            log.info("Recorded dry run for rubric {} v{}: {}",
                    version.getRubricId(), version.getSemver(), out.resultEntity().getStatus());
        }
        return out.envelope();
    }

    private List<CheckOutcome> runSequential(List<RubricCheck> checks, ExecutionContext ctx, IBaseResource resource) {
        List<CheckOutcome> outcomes = new ArrayList<>(checks.size());
        for (RubricCheck c : checks) {
            outcomes.add(runOne(c, ctx, resource));
        }
        return outcomes;
    }

    private List<CheckOutcome> runParallel(List<RubricCheck> checks, ExecutionContext ctx, IBaseResource resource) {
        List<CompletableFuture<CheckOutcome>> futures = checks.stream()
                .map(c -> CompletableFuture.supplyAsync(() -> runOne(c, ctx, resource), checkExecutorPool))
                .collect(Collectors.toList());

        CompletableFuture.allOf(futures.toArray(new CompletableFuture[0])).join();

        return futures.stream()
                .map(CompletableFuture::join)
                .collect(Collectors.toList());
    }

    private CheckOutcome runOne(RubricCheck c, ExecutionContext ctx, IBaseResource resource) {
        long start = System.currentTimeMillis();
        List<RawFinding> findings;

        referenceResolver.bind(ctx.getReferenceIndex());
        try {
            findings = executorRegistry.get(c.getType()).execute(c, ctx);
            findings.forEach(f -> f.setCheckId(c.getCheckId()));
        } catch (Exception e) {
            log.error("Check {} ({}) failed during execution", LogUtils.sanitize(c.getCheckLocalId()), c.getType(), e);
            findings = List.of(RawFinding.builder()
                    .checkId(c.getCheckId())
                    .checkLocalId(c.getCheckLocalId())
                    .dimension(c.getDimension())
                    .severity(Severity.ERROR)
                    .code("check-execution-error")
                    .message("Check execution failed")
                    .location(resource.fhirType())
                    .build());
        } finally {
            referenceResolver.clear();
        }
        long durationMs = System.currentTimeMillis() - start;
        return new CheckOutcome(c.getCheckLocalId(), findings, durationMs);
    }

    private IBaseResource parseResource(JsonNode payload) {
        try {
            return fhirContext.newJsonParser().parseResource(objectMapper.writeValueAsString(payload));
        } catch (Exception e) {
            throw new PayloadParseException("Failed to parse FHIR payload: " + e.getMessage(), e);
        }
    }

    private record CheckSlice(String checkLocalId, int fromIndex, int toIndex) {
        List<EvaluatedFinding> findingsIn(List<EvaluatedFinding> evaluated) {
            return evaluated.subList(fromIndex, toIndex);
        }
    }
}
