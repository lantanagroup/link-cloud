package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.parser.DataFormatException;
import ca.uhn.fhir.parser.IParser;
import ca.uhn.fhir.parser.LenientErrorHandler;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.IValidatorModule;
import ca.uhn.fhir.validation.ResultSeverityEnum;
import ca.uhn.fhir.validation.ValidationResult;
import com.lantanagroup.link.shared.fhir.FhirHelper;
import com.lantanagroup.link.validation.entities.ArtifactEntity;
import com.lantanagroup.link.validation.entities.ResultEntity;
import com.lantanagroup.link.validation.model.ResultModel;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import net.sourceforge.plantuml.StringUtils;
import org.hl7.fhir.common.hapi.validation.support.CachingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.InMemoryTerminologyServerValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.PrePopulatedValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.r4.model.StringType;
import org.hl7.fhir.utilities.npm.NpmPackage;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.List;
import java.util.concurrent.Executors;
import java.util.concurrent.ForkJoinPool;

@Service
public class ValidationService {
    private static final Logger log = LoggerFactory.getLogger(ValidationService.class);
    private final IParser parser;

    private final List<String> allowedResourceTypes = List.of("StructureDefinition", "ValueSet", "CodeSystem", "ImplementationGuide", "Measure", "Library");
    private final ArtifactService artifactService;
    private final ResultRepository resultRepository;
    private FhirValidator validator;
    private PrePopulatedValidationSupport prePopulatedValidationSupport;

    public ValidationService(ArtifactService artifactService, ResultRepository resultRepository) {
        this.artifactService = artifactService;
        this.resultRepository = resultRepository;

        this.parser = FhirHelper.getContext().newJsonParser();
        this.parser.setParserErrorHandler(new LenientErrorHandler(false));

        Executors.newSingleThreadExecutor().submit(this::initArtifacts);
    }

    public void initArtifacts() {
        log.info("Loading artifacts");

        FhirContext fhirContext = FhirHelper.getContext();
        this.prePopulatedValidationSupport = new PrePopulatedValidationSupport(fhirContext);
        ValidationSupportChain validationSupportChain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(fhirContext),
                new InMemoryTerminologyServerValidationSupport(fhirContext),
                this.prePopulatedValidationSupport
        );
        this.validator = fhirContext.newValidator();
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

        log.info("Done loading artifacts into validator");
    }

    public List<ResultModel> validate(Resource resource) {
        log.info("Validating resource");

        ValidationResult validationResult = this.validator.validateWithResult(resource);
        return validationResult.getMessages().stream().map(issue -> {
            ResultModel result = new ResultModel();
            result.setMessage(issue.getMessage());
            result.setExpression(issue.getLocationString());
            result.setSeverity(getIssueSeverity(issue.getSeverity()));
            result.setType(getIssueCode(issue.getMessageId()));

            if (issue.getLocationLine() != null && issue.getLocationCol() != null) {
                result.setLocation(String.format("%d:%d", issue.getLocationLine(), issue.getLocationCol()));
            }

            return result;
        }).toList();
    }

    public OperationOutcome convertToOperationOutcome(List<ResultModel> results) {
        OperationOutcome operationOutcome = new OperationOutcome();

        results.forEach(result -> {
            OperationOutcome.OperationOutcomeIssueComponent issue = operationOutcome.addIssue();
            issue.setDiagnostics(result.getMessage());
            issue.getExpression().add(new StringType(result.getExpression()));
            issue.setSeverity(result.getSeverity());
            issue.setCode(result.getType());

            if (StringUtils.isNotEmpty(result.getLocation())) {
                issue.addLocation(result.getLocation());
            }
        });

        return operationOutcome;
    }

    public void saveResults(List<ResultModel> results, String tenantId, String reportId) {
        this.resultRepository.deleteByTenantIdAndReportId(tenantId, reportId);
        List<ResultEntity> entities = results.stream().map(r -> new ResultEntity(r, tenantId, reportId)).toList();
        this.resultRepository.saveAll(entities);
    }

    private void loadPackage(ArtifactEntity artifactEntity) {
        if (artifactEntity.getType() != ArtifactEntity.Types.PACKAGE) {
            throw new RuntimeException("Artifact is not an NPM package");
        }

        log.info("Loading package into validator {}", artifactEntity.getName());

        try {
            InputStream stream = new ByteArrayInputStream(artifactEntity.getContent());
            NpmPackage npmPackage = NpmPackage.fromPackage(stream);
            List<String> resourceNames = npmPackage.listResources(allowedResourceTypes);

            for (int i = 0; i < resourceNames.size(); i++) {
                log.debug("Loading resource from package {}: {}", artifactEntity.getName(), resourceNames.get(i));
                InputStream resourceContent = npmPackage.loadResource(resourceNames.get(i));

                try {
                    this.loadResource(resourceContent, resourceNames.get(i));
                } catch (DataFormatException e) {
                    log.warn("Error loading resource from package {}: {}", artifactEntity.getName(), resourceNames.get(i), e);
                }
            }
        } catch (IOException e) {
            throw new RuntimeException("Error loading artifact", e);
        }
    }

    private void loadResource(InputStream stream, String name) {
        byte[] content;

        try {
            content = stream.readAllBytes();
        } catch (IOException e) {
            log.error("Error reading resource {}", name, e);
            return;
        }

        String json = new String(content)
                // replace all "\\-------" with "" otherwise it is invalid JSON
                // have seen this pattern several times in libraries to separate sections of the cql
                .replaceAll("\\/\\/\\-+", "")
                // replace all "&copy;" with "" otherwise it is invalid JSON
                .replaceAll("&copy;", "");

        try {
            Resource resource = (Resource) this.parser.parseResource(json);
            this.prePopulatedValidationSupport.addResource(resource);
        } catch (DataFormatException e) {
            log.warn("Error loading resource {} with starting content {}", name, json.substring(0, 100), e);
        }
    }

    private void loadResource(ArtifactEntity artifactEntity) {
        if (artifactEntity.getType() != ArtifactEntity.Types.RESOURCE) {
            throw new RuntimeException("Artifact is not a resource");
        }

        log.info("Loading resource {}", artifactEntity.getName());

        try {
            InputStream stream = new ByteArrayInputStream(artifactEntity.getContent());
            this.loadResource(stream, artifactEntity.getName());
        } catch (DataFormatException e) {
            log.warn("Error loading resource {}", artifactEntity.getName(), e);
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
