package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.IValidatorModule;
import ca.uhn.fhir.validation.ValidationResult;
import com.lantanagroup.link.shared.utils.LogUtils;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.providers.RemoteTermServiceValidation;
import com.lantanagroup.link.validation.providers.ValidationCacheService;
import org.hl7.fhir.common.hapi.validation.support.*;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.stereotype.Service;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.ExecutorService;

/**
 * Holds a single {@link FhirValidator} (and the {@link ValidationSupportChain} whose cache backs it) across
 * {@link #validate} calls, per HAPI's guidance to reuse validator instances. The validator is rebuilt only when
 * {@link ArtifactService} hands back a different {@link ArtifactValidationSupport} -- which it does exactly when
 * an artifact change has invalidated its memoized support -- so uploads and deletes still take effect on the
 * next validation.
 */
@Service
public class ValidationService {
    private static final Logger logger = LoggerFactory.getLogger(ValidationService.class);

    // Raised from HAPI's default of 5000. validateCode results share this cache with the isCodeSystemSupported /
    // isValueSetSupported / fetch entries, and the deployed terminology set carries 45k+ codes, so at 5000 the
    // support checks were evicted even within a single large bundle. Entries are small result objects, so this
    // costs tens of MB at most.
    static final int CHAIN_CACHE_SIZE = 50_000;

    private final FhirContext fhirContext;
    private final ArtifactService artifactService;
    private final LinkConfig linkConfig;
    private final ValidationCacheService validationCacheService;
    private final ValidationResultIgnoreService validationResultIgnoreService;
    private final ExecutorService bundleValidationExecutor;
    private volatile ValidatorState validatorState;

    public ValidationService(
            FhirContext fhirContext,
            ArtifactService artifactService,
            LinkConfig linkConfig,
            ValidationCacheService validationCacheService,
            ValidationResultIgnoreService validationResultIgnoreService,
            @Qualifier("bundleValidationExecutor") ExecutorService bundleValidationExecutor) {
        this.fhirContext = fhirContext;
        this.artifactService = artifactService;
        this.linkConfig = linkConfig;
        this.validationCacheService = validationCacheService;
        this.validationResultIgnoreService = validationResultIgnoreService;
        this.bundleValidationExecutor = bundleValidationExecutor;
    }

    static ValidationSupportChain.CacheConfiguration chainCacheConfiguration() {
        return ValidationSupportChain.CacheConfiguration.defaultValues()
                .setCacheSize(CHAIN_CACHE_SIZE);
    }

    /**
     * Returns the shared validator, rebuilding it if the artifact support has been invalidated since it was built.
     * Validations already in flight keep the validator they started with.
     */
    FhirValidator getValidator() throws IOException {
        ArtifactValidationSupport artifactSupport = artifactService.getValidationSupport();
        ValidatorState state = validatorState;
        if (state == null || state.artifactSupport() != artifactSupport) {
            synchronized (this) {
                state = validatorState;
                if (state == null || state.artifactSupport() != artifactSupport) {
                    state = new ValidatorState(artifactSupport, buildValidator(artifactSupport));
                    validatorState = state;
                }
            }
        }
        return state.validator();
    }

    private FhirValidator buildValidator(ArtifactValidationSupport artifactSupport) {
        logger.info("Building FHIR validator");
        ValidationSupportChain validationSupportChain = new ValidationSupportChain(
                chainCacheConfiguration(),
                new DefaultProfileValidationSupport(fhirContext),
                artifactSupport,
                new SnapshotGeneratingValidationSupport(fhirContext));

        loadTerminologyValidationSupport(fhirContext, linkConfig, validationSupportChain, validationCacheService);

        // The chain caches internally; CachingValidationSupport is deprecated for removal and would only add a
        // second cache layer.
        IValidatorModule validatorModule = new FhirInstanceValidator(validationSupportChain);
        FhirValidator fhirValidator = new FhirValidator(fhirContext);
        fhirValidator.registerValidatorModule(validatorModule);
        fhirValidator.setConcurrentBundleValidation(true);
        fhirValidator.setExecutorService(bundleValidationExecutor);
        return fhirValidator;
    }

    private record ValidatorState(ArtifactValidationSupport artifactSupport, FhirValidator validator) {
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
        return validate(resource, null, null);
    }

    public List<Result> validate(IBaseResource resource, String facilityId, String reportId) {
        try {
            String detail = "FHIR resource";
            if (resource instanceof Bundle bundle) {
                detail = "FHIR bundle " + bundle.getEntry().size() + " entries";
                logger.info("Starting validation of Bundle with {} entries facility={} report={}",
                        bundle.getEntry().size(),
                        LogUtils.sanitize(facilityId),
                        LogUtils.sanitize(reportId));
            }
            ValidationResult validationResult;
            try (ValidationProgressHeartbeat ignored = ValidationProgressHeartbeat.start(logger, detail, facilityId, reportId)) {
                validationResult = getValidator().validateWithResult(resource);
            } catch (IOException ex) {
                throw new UncheckedIOException("Failed to load artifact validation support", ex);
            }
            List<Result> results = validationResult.getMessages().stream()
                    .map(Result::fromMessage)
                    .toList();
            return validationResultIgnoreService.filterIgnored(deduplicateInactiveResults(results));
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
