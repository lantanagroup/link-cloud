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
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import com.lantanagroup.link.validation.services.execution.CheckExecutorRegistry;
import com.lantanagroup.link.validation.services.execution.CheckOutcome;
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
    private final RubricCheckRepository rubricCheckRepository;
    private final RubricVersionRepository rubricVersionRepository;
    private final CheckExecutorRegistry executorRegistry;
    private final ResultEnvelopeAssembler envelopeAssembler;
    private final RubricResultPersister resultPersister;
    private final FhirContext fhirContext;
    private final ObjectMapper objectMapper;
    private final Executor checkExecutorPool;

    private final boolean parallel;

    public RubricExecutionService(RubricVersionResolver versionResolver,
                                  RubricCheckRepository rubricCheckRepository,
                                  RubricVersionRepository rubricVersionRepository,
                                  CheckExecutorRegistry executorRegistry,
                                  ResultEnvelopeAssembler envelopeAssembler,
                                  RubricResultPersister resultPersister,
                                  FhirContext fhirContext,
                                  ObjectMapper objectMapper,
                                  @Qualifier("checkExecutorPool") Executor checkExecutorPool,
                                  @Value("${vaas.checks.parallel:false}") boolean parallel) {
        this.versionResolver = versionResolver;
        this.rubricCheckRepository = rubricCheckRepository;
        this.rubricVersionRepository = rubricVersionRepository;
        this.executorRegistry = executorRegistry;
        this.envelopeAssembler = envelopeAssembler;
        this.resultPersister = resultPersister;
        this.fhirContext = fhirContext;
        this.objectMapper = objectMapper;
        this.checkExecutorPool = checkExecutorPool;
        this.parallel = parallel;
        log.info("Rubric check execution mode: {} (vaas.checks.parallel={})",
                parallel ? "parallel fan-out" : "sequential", parallel);
    }


    public ValidationResultEnvelope evaluate(String rubricId, String semver, EvaluateRequestDto request, boolean persist) {
        OffsetDateTime requestedAt = OffsetDateTime.now();


        RubricVersion version = versionResolver.resolve(rubricId, semver, persist);
        log.debug("Resolved rubric {} v{} ({})", version.getRubricId(), version.getSemver(), version.getRubricVersionId());

        // soft-deleted checks are excluded here
        List<RubricCheck> checks =
                rubricCheckRepository.findByRubricVersionIdAndDeletedFalseOrderByOrdinalAsc(version.getRubricVersionId());

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
                .subject(subject)
                .resource(resource)
                .bundleEntries(bundleEntries)
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
        Map<String, Long> checkDurations = new LinkedHashMap<>();
        for (CheckOutcome outcome : outcomes) {
            allFindings.addAll(outcome.findings());
            checkDurations.put(outcome.checkLocalId(), outcome.durationMs());
        }

        OffsetDateTime completedAt = OffsetDateTime.now();
        ResultEnvelopeAssembler.AssembleOutput out =
                envelopeAssembler.assemble(ctx, version, allFindings, checkDurations, completedAt);

        if (persist) {
            resultPersister.persist(out.resultEntity(), out.findingEntities());
        } else {
            // no result row for dry runs, but the outcome is stored on the version
            // so the publish gate can check it later
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
}
