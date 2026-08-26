package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.IValidatorModule;
import ca.uhn.fhir.validation.SingleValidationMessage;
import ca.uhn.fhir.validation.ValidationResult;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.providers.RemoteTermServiceValidation;
import com.lantanagroup.link.validation.providers.ValidationCacheService;
import org.hl7.fhir.common.hapi.validation.support.*;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.context.annotation.Scope;
import org.springframework.context.annotation.ScopedProxyMode;
import org.springframework.stereotype.Service;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.IOException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Future;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class ValidationService {
    private static final Logger logger = LoggerFactory.getLogger(ValidationService.class);
    private final FhirValidator fhirValidator;
    private final LinkConfig linkConfig;
    private final ExecutorService bundleValidationExecutor;

    public ValidationService(
            FhirContext fhirContext,
            ArtifactService artifactService,
            LinkConfig linkConfig,
            ValidationCacheService validationCacheService,
            @Qualifier("bundleValidationExecutor") ExecutorService bundleValidationExecutor) throws IOException {
        this.linkConfig = linkConfig;
        this.bundleValidationExecutor = bundleValidationExecutor;
        ValidationSupportChain validationSupportChain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(fhirContext),
                artifactService.getValidationSupport(),
                new SnapshotGeneratingValidationSupport(fhirContext));

        loadTerminologyValidationSupport(fhirContext, linkConfig, validationSupportChain, validationCacheService);

        CachingValidationSupport cachingValidationSupport = new CachingValidationSupport(validationSupportChain);
        IValidatorModule validatorModule = new FhirInstanceValidator(cachingValidationSupport);
        fhirValidator = new FhirValidator(fhirContext);
        fhirValidator.registerValidatorModule(validatorModule);
    }

    // Package-private for unit testing of the terminology support chain composition.
    static void loadTerminologyValidationSupport(FhirContext fhirContext, LinkConfig linkConfig, ValidationSupportChain validationSupportChain, ValidationCacheService validationCacheService) {
        if (linkConfig.getFhirTerminologyServiceUrl() != null && !linkConfig.getFhirTerminologyServiceUrl().isEmpty()) {
            var remoteTerm = new RemoteTermServiceValidation(validationCacheService, fhirContext, linkConfig.getFhirTerminologyServiceUrl(), linkConfig.getWhiteListCodeSystemRegex(), linkConfig.getWhiteListValueSetRegex());
            validationSupportChain.addValidationSupport(remoteTerm);
            logger.info("Using remote terminology service at {}", linkConfig.getFhirTerminologyServiceUrl());
        } else if (linkConfig.getTerminologyServiceUrl() != null && !linkConfig.getTerminologyServiceUrl().isEmpty()) {
            // RemoteTerminologyServiceValidationSupport expects the base url to be the root of a FHIR interface
            // Append /api/terminology/fhir to the terminology service URL since this is the link terminology service.
            String terminologyServiceUrl = (linkConfig.getTerminologyServiceUrl().endsWith("/") ? linkConfig.getTerminologyServiceUrl() : linkConfig.getTerminologyServiceUrl() + "/") + "api/terminology/fhir";
            var remoteTerm = new RemoteTermServiceValidation(validationCacheService, fhirContext, terminologyServiceUrl, linkConfig.getWhiteListCodeSystemRegex(), linkConfig.getWhiteListValueSetRegex());
            validationSupportChain.addValidationSupport(remoteTerm);
            logger.info("Using Link terminology service at {}", terminologyServiceUrl);
        } else {
            logger.info("No remote terminology service configured; relying on in-memory terminology support");
        }

        // Always register the in-memory terminology supports as a fallback. A remote terminology service
        // only answers for the valuesets/code systems it owns; base-FHIR and package-owned valuesets (e.g.
        // identifier-use, required code bindings) are validated in-process and have no validator otherwise.
        // The chain consults the remote support first and falls through to these only when it declines.
        validationSupportChain.addValidationSupport(new CommonCodeSystemsTerminologyService(fhirContext));
        validationSupportChain.addValidationSupport(new InMemoryTerminologyServerValidationSupport(fhirContext));
    }

    public List<Result> validate(IBaseResource resource) {
        try {
            List<Result> results = resource instanceof Bundle bundle
                    ? validateBundle(bundle)
                    : toResults(fhirValidator.validateWithResult(resource));
            return deduplicateInactiveResults(results);
        } catch (Exception ex) {
            logger.error("Validation failed", ex);
            throw ex;
        }
    }

    // Chunk the bundle and run up to bundleValidationParallelism chunks at once.
    // Entries inside a chunk are sequential so HAPI never queues the whole NDJSON (HAPI-2246).
    private List<Result> validateBundle(Bundle bundle) {
        List<Bundle.BundleEntryComponent> entries = bundle.getEntry();
        int entryCount = entries.size();
        if (entryCount == 0) {
            return toResults(fhirValidator.validateWithResult(bundle));
        }

        int batchSize = Math.max(1, linkConfig.getBundleValidationBatchSize());
        if (entryCount <= batchSize) {
            return validateSlice(entries, 0, entryCount);
        }

        int parallelism = Math.max(1, linkConfig.getBundleValidationParallelism());
        int chunkCount = (entryCount + batchSize - 1) / batchSize;
        logger.info("Validating Bundle with {} entries in {} chunks of {} on {} threads",
                entryCount, chunkCount, batchSize, parallelism);

        List<Callable<List<Result>>> tasks = new ArrayList<>(chunkCount);
        for (int start = 0; start < entryCount; start += batchSize) {
            int chunkStart = start;
            int chunkEnd = Math.min(start + batchSize, entryCount);
            tasks.add(() -> validateSlice(entries, chunkStart, chunkEnd));
        }

        List<Result> results = new ArrayList<>();
        try {
            for (Future<List<Result>> future : bundleValidationExecutor.invokeAll(tasks)) {
                results.addAll(future.get());
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new RuntimeException("Bundle validation interrupted", e);
        } catch (ExecutionException e) {
            Throwable cause = e.getCause() != null ? e.getCause() : e;
            if (cause instanceof RuntimeException runtimeException) {
                throw runtimeException;
            }
            throw new RuntimeException("Bundle validation failed", cause);
        }
        return results;
    }

    private List<Result> validateSlice(List<Bundle.BundleEntryComponent> entries, int start, int end) {
        List<Result> results = new ArrayList<>();
        for (int i = start; i < end; i++) {
            IBaseResource resource = entries.get(i).getResource();
            if (resource == null) {
                continue;
            }
            ValidationResult validationResult = fhirValidator.validateWithResult(resource);
            String prefix = "Bundle.entry[" + i + "].resource.ofType(" + resource.fhirType() + ")";
            for (SingleValidationMessage message : validationResult.getMessages()) {
                Result result = Result.fromMessage(message);
                result.setExpression(prefixEntryLocation(result.getExpression(), prefix));
                results.add(result);
            }
        }
        return results;
    }

    private static List<Result> toResults(ValidationResult validationResult) {
        return validationResult.getMessages().stream()
                .map(Result::fromMessage)
                .toList();
    }

    // Same path rewrite HAPI uses in FhirValidator.buildValidationMessages.
    static String prefixEntryLocation(String locationString, String bundleEntryPathPrefix) {
        String location = locationString == null ? "" : locationString;
        String currentPath;
        int dotIndex = location.indexOf('.');
        if (dotIndex >= 0) {
            currentPath = location.substring(dotIndex);
        } else if (bundleEntryPathPrefix.isBlank() || location.isBlank()) {
            currentPath = location;
        } else {
            currentPath = "." + location;
        }
        return bundleEntryPathPrefix + currentPath;
    }

    // Text emitted by RemoteTermServiceValidation for an inactive code (see isInactiveIssue).
    private static final String INACTIVE_MARKER = "has a status of inactive and its use should be reviewed.";

    /**
     * HAPI validates a bound coding against both its code system and its bound value set, so an inactive code
     * surfaces the same warning twice for one element (differing only by HAPI's issue code and a trailing
     * "(for 'system#code')" suffix). Collapse those to a single result per element. Only inactive-marker
     * results are considered; every other result is preserved as-is.
     */
    static List<Result> deduplicateInactiveResults(List<Result> results) {
        Set<String> seenInactive = new HashSet<>();
        List<Result> deduplicated = new ArrayList<>(results.size());
        for (Result result : results) {
            String message = result.getMessage();
            if (message != null && message.contains(INACTIVE_MARKER)) {
                String key = result.getExpression() + "|" + result.getLocation() + "|" + normalizeInactiveMessage(message);
                if (!seenInactive.add(key)) {
                    continue;
                }
            }
            deduplicated.add(result);
        }
        return deduplicated;
    }

    // Strip the trailing "(for 'system#code')" that HAPI appends to the code-system-context variant so both
    // variants of the same inactive finding share a key (and distinct inactive codes stay distinct).
    private static String normalizeInactiveMessage(String message) {
        return message.replaceAll("\\s*\\(for '[^']*'\\)\\s*$", "");
    }
}
