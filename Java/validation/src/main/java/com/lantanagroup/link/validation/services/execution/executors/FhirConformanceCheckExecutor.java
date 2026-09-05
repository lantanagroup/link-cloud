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
import org.apache.commons.collections4.ListUtils;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Resource;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import jakarta.annotation.PreDestroy;

import java.io.IOException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.ForkJoinPool;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import java.util.stream.Collectors;

@Component
@Slf4j
public class FhirConformanceCheckExecutor implements CheckExecutor {

    private final FhirValidator fhirValidator;
    private final ObjectMapper objectMapper;

    /**
     * Number of Bundle entries grouped into each sub-Bundle validation call, and (doubling as) the max
     * number of those batch validations run concurrently. Configured value of 0 (the default) means
     * "auto": match {@link ForkJoinPool#commonPool()}'s own parallelism -- this avoids a hardcoded number
     * going stale if the pool's parallelism ever changes (e.g. via the
     * java.util.concurrent.ForkJoinPool.common.parallelism system property). Also sizes
     * {@link #bundleValidationExecutor}.
     */
    private final int batchSize;

    /**
     * Dedicated, fixed-size pool for Bundle batch validations -- isolated from
     * {@link ForkJoinPool#commonPool()} so this (CPU-heavy) work never competes with, or gets
     * starved by, unrelated parallel-stream/CompletableFuture work elsewhere in the app.
     * Concurrency is capped by the pool's fixed size itself, so no separate semaphore is needed.
     */
    private final ExecutorService bundleValidationExecutor;

    public FhirConformanceCheckExecutor(
            FhirValidator fhirValidator,
            ObjectMapper objectMapper,
            @Value("${link.fhir-conformance-check.batch-size:0}") int configuredBatchSize) {
        this.fhirValidator = fhirValidator;
        this.objectMapper = objectMapper;
        this.batchSize = configuredBatchSize > 0 ? configuredBatchSize : ForkJoinPool.commonPool().getParallelism();
        this.bundleValidationExecutor = new ThreadPoolExecutor(
                batchSize, batchSize, 0L, TimeUnit.MILLISECONDS, new LinkedBlockingQueue<>());
    }

    @PreDestroy
    void shutdownBundleValidationExecutor() {
        bundleValidationExecutor.shutdown();
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
     * Splits the Bundle's entries into groups of {@link #batchSize} resources, wraps each group in its
     * own small collection Bundle, and validates each one with a single {@link #validate} call on
     * {@link #bundleValidationExecutor} (which caps concurrent batch validations at {@link #batchSize}).
     * All batches are submitted up front (no batch/wave joins in between) so a slow batch never delays
     * the start of the next one -- excess batches simply queue on the executor until a pool thread frees
     * up. Batching multiple entries per HAPI validation call amortizes the validator's per-call setup
     * cost (e.g. profile/terminology resolution) across the group instead of paying it per entry.
     *
     * <p>{@code options}'s profiles (from the check's parameters) are stamped onto each entry's own
     * {@code meta.profile} rather than passed as {@link ValidationOptions} on the batch call: HAPI only
     * scopes {@link ValidationOptions#addProfile} to the exact resource passed to {@code validate},
     * which here would be the synthetic wrapper Bundle, not each entry. Declaring the profile on
     * {@code meta.profile} instead relies on the validator's normal (always-on) per-resource profile
     * checking, so each entry is still checked against it individually, matching pre-batch behavior.
     */
    private List<RawFinding> executeBundle(RubricCheck check, ExecutionContext context, ValidationOptions options) {
        List<List<IBaseResource>> batches = ListUtils.partition(context.getBundleEntries(), batchSize);

        List<CompletableFuture<List<RawFinding>>> futures = batches.stream()
                .map(batch -> CompletableFuture.supplyAsync(
                        () -> validate(check, toBatchBundle(batch, options), new ValidationOptions()), bundleValidationExecutor))
                .collect(Collectors.toList());

        return futures.stream()
                .map(CompletableFuture::join)
                .flatMap(List::stream)
                .collect(Collectors.toList());
    }

    /**
     * Wraps a group of Bundle entries in a new collection-type Bundle so they can be validated together
     * in a single HAPI {@link #validate} call. Each entry is deep-copied before its profiles are stamped
     * on so the mutation never touches the original resource objects held by {@link ExecutionContext}
     * (shared across all checks running against this request). Entries are known to be R4 resources here
     * since they were extracted from an R4 {@link Bundle} in {@link #execute}.
     */
    private static Bundle toBatchBundle(List<IBaseResource> batch, ValidationOptions options) {
        Bundle batchBundle = new Bundle();
        batchBundle.setType(Bundle.BundleType.COLLECTION);
        for (IBaseResource entry : batch) {
            Resource resource = ((Resource) entry).copy();
            for (String profile : options.getProfiles()) {
                resource.getMeta().addProfile(profile);
            }
            batchBundle.addEntry().setResource(resource);
        }
        return batchBundle;
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
