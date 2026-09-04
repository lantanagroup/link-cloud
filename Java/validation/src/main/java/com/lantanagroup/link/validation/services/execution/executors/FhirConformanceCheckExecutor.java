package com.lantanagroup.link.validation.services.execution.executors;

import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.SingleValidationMessage;
import ca.uhn.fhir.validation.ValidationOptions;
import ca.uhn.fhir.validation.ValidationResult;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.execution.CheckExecutor;
import com.lantanagroup.link.validation.services.execution.UnresolvedBindingClassifier;
import lombok.extern.slf4j.Slf4j;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.io.IOException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.ForkJoinPool;
import java.util.concurrent.Semaphore;
import java.util.stream.Collectors;

@Component
@Slf4j
public class FhirConformanceCheckExecutor implements CheckExecutor {

    private final FhirValidator fhirValidator;
    private final ObjectMapper objectMapper;

    /**
     * Max concurrent Bundle-entry validations. Configured value of 0 (the default) means "auto":
     * match {@link ForkJoinPool#commonPool()}'s own parallelism, since that is the pool entry
     * validations actually run on -- this avoids a hardcoded number going stale if the pool's
     * parallelism ever changes (e.g. via the java.util.concurrent.ForkJoinPool.common.parallelism
     * system property).
     */
    private final int batchSize;

    public FhirConformanceCheckExecutor(
            FhirValidator fhirValidator,
            ObjectMapper objectMapper,
            @Value("${link.fhir-conformance-check.batch-size:0}") int configuredBatchSize) {
        this.fhirValidator = fhirValidator;
        this.objectMapper = objectMapper;
        this.batchSize = configuredBatchSize > 0 ? configuredBatchSize : ForkJoinPool.commonPool().getParallelism();
    }

    @Override
    public CheckType supports() {
        return CheckType.FHIR_CONFORMANCE;
    }

    @Override
    public List<RawFinding> execute(RubricCheck check, ExecutionContext context) {
        ValidationOptions options = buildOptions(check);
        IBaseResource resource = context.getResource();

        if (resource instanceof Bundle) {
            return executeBundle(check, context, options);
        }
        return validate(check, resource, options);
    }

    private ValidationOptions buildOptions(RubricCheck check) {
        ValidationOptions options = new ValidationOptions();
        if (check.getParametersJson() != null) {
            try {
                JsonNode params = objectMapper.readTree(check.getParametersJson());
                JsonNode profiles = params.path("profiles");
                if (profiles.isArray()) {
                    profiles.forEach(p -> options.addProfile(p.asText()));
                }
            } catch (IOException e) {
                log.warn("Failed to parse FHIR_CONFORMANCE parameters for check {}: {}", check.getCheckLocalId(), e.getMessage());
            }
        }
        return options;
    }

    /**
     * Validates each Bundle entry as its own independent resource, capping concurrent validations at
     * {@link #batchSize} via a {@link Semaphore}. All entries are submitted up front (no batch/wave
     * joins in between) so a slow resource never delays the start of the next one -- only the number
     * of validations actually running at once is bounded.
     */
    private List<RawFinding> executeBundle(RubricCheck check, ExecutionContext context, ValidationOptions options) {
        Semaphore semaphore = new Semaphore(batchSize);
        List<CompletableFuture<List<RawFinding>>> futures = context.getBundleEntries().stream()
                .map(entry -> CompletableFuture.supplyAsync(() -> validateThrottled(check, entry, options, semaphore),
                        ForkJoinPool.commonPool()))
                .collect(Collectors.toList());

        return futures.stream()
                .map(CompletableFuture::join)
                .flatMap(List::stream)
                .collect(Collectors.toList());
    }

    private List<RawFinding> validateThrottled(RubricCheck check, IBaseResource resource, ValidationOptions options, Semaphore semaphore) {
        try {
            semaphore.acquire();
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new CompletionException(e);
        }
        try {
            return validate(check, resource, options);
        } finally {
            semaphore.release();
        }
    }

    private List<RawFinding> validate(RubricCheck check, IBaseResource resource, ValidationOptions options) {
        ValidationResult result = fhirValidator.validateWithResult(resource, options);
        List<SingleValidationMessage> messages = result.getMessages();
        log.info("HAPI validation (rubric engine) returned {} messages for check {}", messages.size(), check.getCheckLocalId());

        Set<String> unresolvedLocations = new HashSet<>();
        for (SingleValidationMessage msg : messages) {
            if (isTerminologyMessage(msg)
                    && UnresolvedBindingClassifier.isUnresolvedConformanceMessage(msg.getMessageId(), msg.getMessage())
                    && msg.getLocationString() != null) {
                unresolvedLocations.add(msg.getLocationString());
            }
        }

        List<RawFinding> findings = new ArrayList<>();
        for (SingleValidationMessage msg : messages) {
            if (isNotEvaluated(msg, unresolvedLocations)) {
                findings.add(RawFinding.builder()
                        .checkLocalId(check.getCheckLocalId())
                        .dimension(check.getDimension())
                        .severity(Severity.INFORMATION)
                        .notEvaluated(true)
                        .code("binding-not-evaluated")
                        .message(msg.getMessage())
                        .location(msg.getLocationString())
                        .expression(msg.getLocationString())
                        .build());
                continue;
            }
            findings.add(RawFinding.builder()
                    .checkLocalId(check.getCheckLocalId())
                    .dimension(check.getDimension())
                    .severity(mapSeverity(msg))
                    .code("fhir-conformance")
                    .message(msg.getMessage())
                    .location(msg.getLocationString())
                    .expression(msg.getLocationString())
                    .build());
        }
        return findings;
    }

    /**
     * Whether a validation message describes a terminology binding that could not actually be
     * evaluated. A direct resolution/expansion failure always qualifies; a membership failure qualifies
     * only when it is co-located with a resolution failure (it is then a consequence of it). Gated on
     * the message id containing "terminology" so structural conformance problems (unknown extension,
     * slice mismatch, cardinality, …) are never downgraded even if their text contains a resolution-like
     * phrase. Classification keys on HAPI's stable message id rather than the (localizable) text — see
     * {@link UnresolvedBindingClassifier}.
     */
    private static boolean isNotEvaluated(SingleValidationMessage msg, Set<String> unresolvedLocations) {
        if (!isTerminologyMessage(msg)) {
            return false;
        }
        if (UnresolvedBindingClassifier.isUnresolvedConformanceMessage(msg.getMessageId(), msg.getMessage())) {
            return true;
        }
        if (UnresolvedBindingClassifier.isMembershipFailure(msg.getMessageId(), msg.getMessage())) {
            String location = msg.getLocationString();
            return location != null && isCoLocatedWithUnresolved(location, unresolvedLocations);
        }
        return false;
    }

    private static boolean isTerminologyMessage(SingleValidationMessage msg) {
        String messageId = msg.getMessageId();
        return messageId != null && messageId.toLowerCase().contains("terminology");
    }

    /**
     * Segment-boundary-aware co-location: {@code location} shares an element path with a resolution
     * failure — the same element, or one is an ancestor of the other (e.g. the resolution failure
     * reported at {@code Observation.code} and the membership failure at {@code Observation.code.coding[0]}).
     * A plain prefix test would wrongly match {@code Observation.code} against {@code Observation.codeableConcept},
     * so path boundaries ('.' / '[') are required.
     */
    private static boolean isCoLocatedWithUnresolved(String location, Set<String> unresolvedLocations) {
        for (String unresolved : unresolvedLocations) {
            if (location.equals(unresolved)
                    || isDescendantPath(location, unresolved)
                    || isDescendantPath(unresolved, location)) {
                return true;
            }
        }
        return false;
    }

    private static boolean isDescendantPath(String child, String ancestor) {
        return child.startsWith(ancestor + ".") || child.startsWith(ancestor + "[");
    }

    private Severity mapSeverity(SingleValidationMessage msg) {
        if (msg.getSeverity() == null) {
            return Severity.INFORMATION;
        }
        return switch (msg.getSeverity()) {
            case ERROR, FATAL -> Severity.ERROR;
            case WARNING -> Severity.WARNING;
            case INFORMATION -> Severity.INFORMATION;
        };
    }
}
