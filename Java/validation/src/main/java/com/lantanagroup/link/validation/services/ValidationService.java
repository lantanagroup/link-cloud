package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.parser.DataFormatException;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.IValidatorModule;
import ca.uhn.fhir.validation.ResultSeverityEnum;
import com.lantanagroup.link.shared.fhir.FhirHelper;
import com.lantanagroup.link.validation.entities.Artifact;
import org.hl7.fhir.common.hapi.validation.support.CachingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.InMemoryTerminologyServerValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.PrePopulatedValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.utilities.npm.NpmPackage;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.List;
import java.util.concurrent.ForkJoinPool;

@Service
public class ValidationService {
    private static final Logger log = LoggerFactory.getLogger(ValidationService.class);

    private final List<String> allowedResourceTypes = List.of("StructureDefinition", "ValueSet", "CodeSystem", "ImplementationGuide", "Measure", "Library");
    private final ArtifactService artifactService;
    private FhirValidator validator;
    private PrePopulatedValidationSupport prePopulatedValidationSupport;

    public ValidationService(ArtifactService artifactService) {
        this.artifactService = artifactService;
        this.initialize();
    }

    public void initialize() {
        log.info("Loading artifacts");

        this.prePopulatedValidationSupport = new PrePopulatedValidationSupport(FhirHelper.getContext());
        ValidationSupportChain validationSupportChain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(FhirHelper.getContext()),
                new InMemoryTerminologyServerValidationSupport(FhirHelper.getContext()),
                this.prePopulatedValidationSupport
        );
        this.validator = FhirHelper.getContext().newValidator();
        this.validator.setExecutorService(ForkJoinPool.commonPool());
        IValidatorModule module = new FhirInstanceValidator(new CachingValidationSupport(validationSupportChain));
        this.validator.registerValidatorModule(module);
        this.validator.setConcurrentBundleValidation(true);

        this.artifactService.getArtifacts().forEach(a -> {
            switch (a.getType()) {
                case PACKAGE -> this.loadPackage(a);
                case RESOURCE -> this.loadResource(a);
            }
        });
    }

    private void loadPackage(Artifact artifact) {
        if (artifact.getType() != Artifact.Types.PACKAGE) {
            throw new RuntimeException("Artifact is not an NPM package");
        }

        log.info("Loading package {}", artifact.getName());

        try {
            InputStream stream = new ByteArrayInputStream(artifact.getContent());
            NpmPackage npmPackage = NpmPackage.fromPackage(stream);
            List<String> resourceNames = npmPackage.listResources(allowedResourceTypes);

            for (int i = 0; i < resourceNames.size(); i++) {
                log.trace("Loading resource from package {}: {}", artifact.getName(), resourceNames.get(i));
                InputStream resourceContent = npmPackage.loadResource(resourceNames.get(i));

                try {
                    this.loadResource(resourceContent);
                } catch (DataFormatException e) {
                    log.warn("Error loading resource from package {}: {}", artifact.getName(), resourceNames.get(i), e);
                }
            }
        } catch (IOException e) {
            throw new RuntimeException("Error loading artifact", e);
        }
    }

    private void loadResource(InputStream stream) {
        try {
            byte[] content = stream.readAllBytes();
            // replace all "\\-------" with "" otherwise it is invalid JSON
            String json = new String(content).replaceAll("\\/\\/\\-+", "");
            Resource resource = FhirHelper.deserialize(json);
            this.prePopulatedValidationSupport.addResource(resource);
        } catch (DataFormatException e) {
            log.warn("Error loading resource", e);
        } catch (IOException e) {
            log.error("Error reading resource", e);
        }
    }

    private void loadResource(Artifact artifact) {
        if (artifact.getType() != Artifact.Types.RESOURCE) {
            throw new RuntimeException("Artifact is not a resource");
        }

        log.info("Loading resource {}", artifact.getName());

        try {
            InputStream stream = new ByteArrayInputStream(artifact.getContent());
            this.loadResource(stream);
        } catch (DataFormatException e) {
            log.warn("Error loading resource {}", artifact.getName(), e);
        }
    }

    private static OperationOutcome.IssueSeverity getIssueSeverity(ResultSeverityEnum severity) {
        return switch (severity) {
            case ERROR -> OperationOutcome.IssueSeverity.ERROR;
            case WARNING -> OperationOutcome.IssueSeverity.WARNING;
            case INFORMATION -> OperationOutcome.IssueSeverity.INFORMATION;
            case FATAL -> OperationOutcome.IssueSeverity.FATAL;
            default -> throw new RuntimeException("Unexpected severity " + severity);
        };
    }

    private static OperationOutcome.IssueType getIssueCode(String messageId) {
        if (messageId == null) {
            return OperationOutcome.IssueType.NULL;
        } else if (messageId.startsWith("Rule ")) {
            return OperationOutcome.IssueType.INVARIANT;
        }

        return switch (messageId) {
            case "TERMINOLOGY_TX_SYSTEM_NO_CODE" -> OperationOutcome.IssueType.INFORMATIONAL;
            case "Terminology_TX_NoValid_2_CC", "Terminology_PassThrough_TX_Message",
                 "Terminology_TX_Code_ValueSet_Ext", "Terminology_TX_NoValid_17", "Terminology_TX_NoValid_16",
                 "Terminology_TX_NoValid_3_CC" -> OperationOutcome.IssueType.CODEINVALID;
            case "Extension_EXT_Unknown" -> OperationOutcome.IssueType.EXTENSION;
            case "Measure_MR_M_NotFound" -> OperationOutcome.IssueType.NOTFOUND;
            case "Validation_VAL_Profile_Minimum", "Validation_VAL_Profile_Maximum", "Extension_EXT_Type",
                 "Validation_VAL_Profile_Unknown", "Reference_REF_NoDisplay" -> OperationOutcome.IssueType.STRUCTURE;
            case "Type_Specific_Checks_DT_String_WS" -> OperationOutcome.IssueType.VALUE;
            case "Terminology_TX_System_Unknown" -> OperationOutcome.IssueType.UNKNOWN;
            case "Type_Specific_Checks_DT_Code_WS" -> OperationOutcome.IssueType.INVALID;
            default -> OperationOutcome.IssueType.NULL;
        };
    }
}
