package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.context.support.IValidationSupport;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.IValidatorModule;
import ca.uhn.fhir.validation.ValidationResult;
import com.lantanagroup.link.validation.configs.LinkConfig;
import com.lantanagroup.link.validation.entities.Result;
import org.hl7.fhir.common.hapi.validation.support.*;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.context.annotation.Scope;
import org.springframework.context.annotation.ScopedProxyMode;
import org.springframework.stereotype.Service;

import java.io.IOException;
import java.util.List;
import java.util.concurrent.ForkJoinPool;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class ValidationService {
    private final FhirValidator fhirValidator;

    public ValidationService(FhirContext fhirContext, ArtifactService artifactService, LinkConfig linkConfig) throws IOException {
        ValidationSupportChain validationSupportChain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(fhirContext),
                artifactService.getValidationSupport(),
                new SnapshotGeneratingValidationSupport(fhirContext),
                new CommonCodeSystemsTerminologyService(fhirContext),
                getUnknownCodeSystemWarningValidationSupport(fhirContext),
                getTerminologyValidationSupport(fhirContext, linkConfig));

        CachingValidationSupport cachingValidationSupport = new CachingValidationSupport(validationSupportChain);
        IValidatorModule validatorModule = new FhirInstanceValidator(cachingValidationSupport);
        fhirValidator = new FhirValidator(fhirContext);
        fhirValidator.registerValidatorModule(validatorModule);
        fhirValidator.setConcurrentBundleValidation(true);
        fhirValidator.setExecutorService(ForkJoinPool.commonPool());
    }

    private static IValidationSupport getTerminologyValidationSupport(FhirContext fhirContext, LinkConfig linkConfig) {
        if (linkConfig.getFhirTerminologyServiceUrl() != null && !linkConfig.getFhirTerminologyServiceUrl().isEmpty()) {
            return new RemoteTerminologyServiceValidationSupport(fhirContext, linkConfig.getFhirTerminologyServiceUrl());
        } else if (linkConfig.getTerminologyServiceUrl() != null && !linkConfig.getTerminologyServiceUrl().isEmpty()) {
            // RemoteTerminologyServiceValidationSupport expects the base url to be the root of a FHIR interface
            // Append /api/terminology/fhir to the terminology service URL since this is the link terminology service.
            String terminologyServiceUrl = (linkConfig.getTerminologyServiceUrl().endsWith("/") ? linkConfig.getTerminologyServiceUrl() : linkConfig.getTerminologyServiceUrl() + "/") + "api/terminology/fhir";
            return new RemoteTerminologyServiceValidationSupport(fhirContext, terminologyServiceUrl);
        } else {
            return new InMemoryTerminologyServerValidationSupport(fhirContext);
        }
    }

    private static UnknownCodeSystemWarningValidationSupport getUnknownCodeSystemWarningValidationSupport(
            FhirContext fhirContext) {
        UnknownCodeSystemWarningValidationSupport validationSupport =
                new UnknownCodeSystemWarningValidationSupport(fhirContext);
        validationSupport.setNonExistentCodeSystemSeverity(IValidationSupport.IssueSeverity.WARNING);
        return validationSupport;
    }

    public List<Result> validate(IBaseResource resource) {
        ValidationResult validationResult = fhirValidator.validateWithResult(resource);
        return validationResult.getMessages().stream()
                .map(Result::fromMessage)
                .toList();
    }
}
