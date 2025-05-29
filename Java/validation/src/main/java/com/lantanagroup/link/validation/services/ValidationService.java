package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.context.support.IValidationSupport;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.IValidatorModule;
import ca.uhn.fhir.validation.ValidationResult;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Result;
import org.hl7.fhir.common.hapi.validation.support.*;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.context.annotation.Scope;
import org.springframework.context.annotation.ScopedProxyMode;
import org.springframework.stereotype.Service;

import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.Reader;
import java.util.List;
import java.util.concurrent.ForkJoinPool;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class ValidationService {
    private final FhirValidator fhirValidator;

    public ValidationService(FhirContext fhirContext, ArtifactService artifactService) throws IOException {
        ValidationSupportChain validationSupportChain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(fhirContext),
                artifactService.getValidationSupport(),
                new SnapshotGeneratingValidationSupport(fhirContext),
                new InMemoryTerminologyServerValidationSupport(fhirContext),
                new CommonCodeSystemsTerminologyService(fhirContext),
                getUnknownCodeSystemWarningValidationSupport(fhirContext));
        CachingValidationSupport cachingValidationSupport = new CachingValidationSupport(validationSupportChain);
        IValidatorModule validatorModule = new FhirInstanceValidator(cachingValidationSupport);
        fhirValidator = new FhirValidator(fhirContext);
        fhirValidator.registerValidatorModule(validatorModule);
        fhirValidator.setConcurrentBundleValidation(true);
        fhirValidator.setExecutorService(ForkJoinPool.commonPool());
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

    public String generatePrequalReport(List<Result> results) throws IOException {

        if (results != null && results.isEmpty()) {
            return null;
        }

        ObjectMapper mapper = new ObjectMapper();

        try (InputStream is = this.getClass().getClassLoader().getResourceAsStream("prequal-report.html")) {
            String json = mapper.writeValueAsString(results);
            String html = readInputStream(is);
            return html.replace("var report = {};", "var report = " + json + ";");
        }
    }

    private String readInputStream(InputStream is) throws IOException {
        Reader inputStreamReader = new InputStreamReader(is);
        StringBuilder sb = new StringBuilder();

        int data = inputStreamReader.read();
        while (data != -1) {
            sb.append((char) data);
            data = inputStreamReader.read();
        }

        inputStreamReader.close();

        return sb.toString();
    }
}
