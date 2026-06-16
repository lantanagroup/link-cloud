package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.validation.FhirValidator;
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


    public ValidationService(FhirContext fhirContext,
                             ArtifactService artifactService,
                             LinkConfig linkConfig,
                             ValidationCacheService validationCacheService,
                             CategoryBackedPolicyAdvisor policyAdvisor) throws IOException {
        ValidationSupportChain validationSupportChain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(fhirContext),
                artifactService.getValidationSupport(),
                new SnapshotGeneratingValidationSupport(fhirContext));

        loadTerminologyValidationSupport(fhirContext, linkConfig, validationSupportChain, validationCacheService);

        CachingValidationSupport cachingValidationSupport = new CachingValidationSupport(validationSupportChain);
        FhirInstanceValidator instanceValidator = new FhirInstanceValidator(cachingValidationSupport);
        // Consulted before terminology validation runs. SKIP-strategy categories short-circuit
        // here so unrecognized code systems never hit the terminology service. All other policy
        // decisions inherit FhirDefaultPolicyAdvisor's behaviour.
        instanceValidator.setValidatorPolicyAdvisor(policyAdvisor);
        fhirValidator = new FhirValidator(fhirContext);
        fhirValidator.registerValidatorModule(instanceValidator);
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
        try {
            ValidationResult validationResult = fhirValidator.validateWithResult(resource);
            return validationResult.getMessages().stream()
                    .map(Result::fromMessage)
                    .toList();
        } catch (Exception ex) {
            logger.error("Validation failed", ex);
            throw ex;
        }
    }
}
