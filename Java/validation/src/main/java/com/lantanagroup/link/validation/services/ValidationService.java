package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.IValidatorModule;
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
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.concurrent.ExecutorService;
import java.util.stream.Collectors;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class ValidationService {
    private static final Logger logger = LoggerFactory.getLogger(ValidationService.class);
    private final FhirValidator fhirValidator;
    private final ValidationResultIgnoreService validationResultIgnoreService;

    public ValidationService(
            FhirContext fhirContext,
            ArtifactService artifactService,
            LinkConfig linkConfig,
            ValidationCacheService validationCacheService,
            ValidationResultIgnoreService validationResultIgnoreService,
            @Qualifier("bundleValidationExecutor") ExecutorService bundleValidationExecutor) throws IOException {
        this.validationResultIgnoreService = validationResultIgnoreService;
        ValidationSupportChain validationSupportChain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(fhirContext),
                artifactService.getValidationSupport(),
                new SnapshotGeneratingValidationSupport(fhirContext));

        loadTerminologyValidationSupport(fhirContext, linkConfig, validationSupportChain, validationCacheService);

        CachingValidationSupport cachingValidationSupport = new CachingValidationSupport(validationSupportChain);
        IValidatorModule validatorModule = new FhirInstanceValidator(cachingValidationSupport);
        fhirValidator = new FhirValidator(fhirContext);
        fhirValidator.registerValidatorModule(validatorModule);
        fhirValidator.setConcurrentBundleValidation(true);
        fhirValidator.setExecutorService(bundleValidationExecutor);
    }

    // Public so FhirConfig can compose the rubric engine's chain from the same terminology
    // supports (same remote support, same order, same whitelists) as this legacy validator.
    public static void loadTerminologyValidationSupport(FhirContext fhirContext, LinkConfig linkConfig, ValidationSupportChain validationSupportChain, ValidationCacheService validationCacheService) {
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
            if (resource instanceof Bundle bundle) {
                logger.info("Starting validation of Bundle with {} entries", bundle.getEntry().size());
            }
            ValidationResult validationResult = fhirValidator.validateWithResult(resource);
            List<Result> results = validationResult.getMessages().stream()
                    .map(Result::fromMessage)
                    .toList();
            List<Result> deduplicated = deduplicateInactiveResults(results);
            Map<String, Long> bySeverity = deduplicated.stream()
                    .collect(Collectors.groupingBy(
                            r -> String.valueOf(r.getSeverity()), TreeMap::new, Collectors.counting()));
            logger.info("HAPI validation (legacy) returned {} findings ({} after inactive-dedup); severities {} are raw HAPI output — the legacy path applies no category/severity overrides",
                    results.size(), deduplicated.size(), bySeverity);
            return validationResultIgnoreService.filterIgnored(deduplicated);
        } catch (Exception ex) {
            logger.error("Validation failed", ex);
            throw ex;
        }
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
