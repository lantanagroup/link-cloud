package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.IValidatorModule;
import ca.uhn.fhir.validation.ValidationResult;
import com.lantanagroup.link.shared.Timer;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.providers.RemoteTermServiceValidation;
import com.lantanagroup.link.validation.providers.ValidationCacheService;
import org.hl7.fhir.common.hapi.validation.support.*;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.context.annotation.Scope;
import org.springframework.context.annotation.ScopedProxyMode;
import org.springframework.stereotype.Service;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.IOException;
import java.util.List;
import java.util.concurrent.ForkJoinPool;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class ValidationService {
    private static final Logger logger = LoggerFactory.getLogger(ValidationService.class);
    private final FhirValidator fhirValidator;
    private final MetricService metricService;


    public ValidationService(FhirContext fhirContext, ArtifactService artifactService, LinkConfig linkConfig, ValidationCacheService validationCacheService, MetricService metricService) throws IOException {
        this.metricService = metricService;

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
        fhirValidator.setExecutorService(ForkJoinPool.commonPool());
    }

    private static void loadTerminologyValidationSupport(FhirContext fhirContext, LinkConfig linkConfig, ValidationSupportChain validationSupportChain, ValidationCacheService validationCacheService) {
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
            var commonCodeSystemsTerminologyService = new CommonCodeSystemsTerminologyService(fhirContext);
            var inMemTerm = new InMemoryTerminologyServerValidationSupport(fhirContext);

            validationSupportChain.addValidationSupport(commonCodeSystemsTerminologyService);
            validationSupportChain.addValidationSupport(inMemTerm);
            logger.info("Using in-memory terminology service");
        }
    }

    public List<Result> validate(IBaseResource resource) {
        try (Timer timer = Timer.start()) {
            try {
                ValidationResult validationResult = fhirValidator.validateWithResult(resource);

                this.metricService.getValidationDurationUpDown().add((long) timer.getSeconds());
                this.metricService.getValidationResultsCounter().add(validationResult.getMessages().size());

                logger.debug("Validation completed with {} results in {} seconds", validationResult.getMessages().size(), String.format("%.2f", timer.getSeconds()));

                return validationResult.getMessages().stream()
                        .map(Result::fromMessage)
                        .toList();
            } catch (Exception ex) {
                if (isHapi2509(ex)) {
                    // Workaround for HAPI FHIR bug https://github.com/hapifhir/hapi-fhir/issues/7200
                    // MeasureValidator.validateMeasureReport() calls fetchResourcesByUrl() which is
                    // unimplemented in WorkerContextValidationSupportAdapter (HAPI-2509).
                    // With concurrent validation, this may be wrapped in ExecutionException.
                    throw new ValidationSkippedException(
                            "Validation skipped due to known HAPI bug (HAPI-2509): " + ex.getMessage(), ex);
                }
                logger.error("Validation failed", ex);
                throw ex;
            }
        }
    }

    private static boolean isHapi2509(Throwable ex) {
        for (Throwable t = ex; t != null; t = t.getCause()) {
            if (t.getMessage() != null && t.getMessage().contains("HAPI-2509")) {
                return true;
            }
        }
        return false;
    }
}
