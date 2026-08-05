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
import com.lantanagroup.link.validation.enums.Severity;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.stereotype.Service;

import java.time.OffsetDateTime;
import java.util.*;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
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


    public ValidationResultEnvelope evaluate(String rubricId, String semver, EvaluateRequestDto request, boolean persist) {
        OffsetDateTime requestedAt = OffsetDateTime.now();


        RubricVersion version = versionResolver.resolve(rubricId, semver, persist);
        log.debug("Resolved rubric {} v{} ({})", version.getRubricId(), version.getSemver(), version.getRubricVersionId());

        List<RubricCheck> checks = rubricCheckRepository.findByRubricVersionIdOrderByOrdinalAsc(version.getRubricVersionId());

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

        List<RawFinding> allFindings = new ArrayList<>();
        Map<String, Long> checkDurations = new LinkedHashMap<>();
        for (RubricCheck c : checks) {
            if (!c.isEnabled()) continue;
            long start = System.currentTimeMillis();
            try {
                List<RawFinding> findings = executorRegistry.get(c.getType()).execute(c, ctx);
                findings.forEach(f -> f.setCheckId(c.getCheckId()));
                allFindings.addAll(findings);
            } catch (Exception e) {
                log.error("Check {} ({}) failed during execution", c.getCheckLocalId(), c.getType(), e);
                allFindings.add(RawFinding.builder()
                        .checkId(c.getCheckId())
                        .checkLocalId(c.getCheckLocalId())
                        .dimension(c.getDimension())
                        .severity(Severity.ERROR)
                        .code("check-execution-error")
                        .message("Check executor threw: " + e.getMessage())
                        .location(resource.fhirType())
                        .build());
            }
            checkDurations.put(c.getCheckLocalId(), System.currentTimeMillis() - start);
        }

        OffsetDateTime completedAt = OffsetDateTime.now();
        ResultEnvelopeAssembler.AssembleOutput out =
                envelopeAssembler.assemble(ctx, version, allFindings, checkDurations, completedAt);

        if (persist) {
            resultPersister.persist(out.resultEntity(), out.findingEntities());
        } else {
            // Dry run: no result row, but record the outcome on the version so the
            // dry-run publish gate (link.rubric.dry-run.required-for-publish) can read it.
            rubricVersionRepository.recordDryRun(
                    version.getRubricVersionId(), out.resultEntity().getStatus(), completedAt);
            log.info("Recorded dry run for rubric {} v{}: {}",
                    version.getRubricId(), version.getSemver(), out.resultEntity().getStatus());
        }
        return out.envelope();
    }

    private IBaseResource parseResource(JsonNode payload) {
        try {
            return fhirContext.newJsonParser().parseResource(objectMapper.writeValueAsString(payload));
        } catch (Exception e) {
            throw new PayloadParseException("Failed to parse FHIR payload: " + e.getMessage(), e);
        }
    }
}
