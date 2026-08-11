package com.lantanagroup.link.validation.services.execution.executors;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.ConceptValidationOptions;
import ca.uhn.fhir.context.support.IValidationSupport;
import ca.uhn.fhir.context.support.ValidationSupportContext;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.execution.CheckExecutor;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Coding;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;

@Component
@RequiredArgsConstructor
@Slf4j
public class TerminologyCheckExecutor implements CheckExecutor {

    private final FhirContext fhirContext;
    private final ValidationSupportChain validationSupportChain;
    private final ObjectMapper objectMapper;

    @Override
    public CheckType supports() {
        return CheckType.TERMINOLOGY;
    }

    @Override
    public List<RawFinding> execute(RubricCheck check, ExecutionContext context) {
        JsonNode params = parseParams(check);
        boolean validateCodings = params == null || params.path("validateCodings").asBoolean(true);
        if (!validateCodings) {
            return List.of();
        }

        Pattern whitelist = null;
        if (params != null && params.hasNonNull("valueSetWhitelistRegex")) {
            try {
                whitelist = Pattern.compile(params.get("valueSetWhitelistRegex").asText());
            } catch (Exception e) {
                log.warn("TERMINOLOGY check {} has invalid valueSetWhitelistRegex: {}",
                        check.getCheckLocalId(), e.getMessage());
            }
        }

        Severity severity = check.getSeverityOverride() != null ? check.getSeverityOverride() : Severity.WARNING;
        ValidationSupportContext supportContext = new ValidationSupportContext(validationSupportChain);
        ConceptValidationOptions options = new ConceptValidationOptions();

        List<IBaseResource> targets = context.getBundleEntries().isEmpty()
                ? List.of(context.getResource())
                : context.getBundleEntries();

        log.info("TERMINOLOGY check '{}': sweeping {} resource(s) for Coding elements; codes are validated through the ValidationSupportChain",
                check.getCheckLocalId(), targets.size());

        int checked = 0;
        int invalid = 0;
        int skipped = 0;
        List<RawFinding> findings = new ArrayList<>();
        for (IBaseResource resource : targets) {
            List<Coding> codings = fhirContext.newTerser().getAllPopulatedChildElementsOfType(resource, Coding.class);
            for (Coding coding : codings) {
                String system = coding.getSystem();
                String code = coding.getCode();
                if (system == null || system.isBlank() || code == null || code.isBlank()) {
                    continue;
                }
                if (whitelist != null && whitelist.matcher(system).matches()) {
                    log.info("TERMINOLOGY check '{}': code {}|{} skipped (system matches valueSetWhitelistRegex)",
                            check.getCheckLocalId(), system, code);
                    skipped++;
                    continue;
                }

                checked++;
                IValidationSupport.CodeValidationResult result;
                try {
                    result = validationSupportChain.validateCode(supportContext, options, system, code, coding.getDisplay(), null);
                } catch (Exception e) {
                    log.info("TERMINOLOGY check '{}': validateCode threw for {}|{} — skipping this code: {}",
                            check.getCheckLocalId(), system, code, e.getMessage());
                    skipped++;
                    continue;
                }
                if (result == null) {
                    log.info("TERMINOLOGY check '{}': no validation support could answer for system {} (code {}) — SKIPPED (counts as pass)",
                            check.getCheckLocalId(), system, code);
                    skipped++;
                    continue;
                }
                if (result.isOk()) {
                    log.info("TERMINOLOGY check '{}': code {}|{} (display '{}') on {} -> VALID",
                            check.getCheckLocalId(), system, code, coding.getDisplay(), location(resource));
                } else {
                    invalid++;
                    log.info("TERMINOLOGY check '{}': code {}|{} (display '{}') on {} -> INVALID: {}",
                            check.getCheckLocalId(), system, code, coding.getDisplay(), location(resource), result.getMessage());
                }
                if (!result.isOk()) {
                    String detail = result.getMessage() != null ? ": " + result.getMessage() : "";
                    findings.add(RawFinding.builder()
                            .checkLocalId(check.getCheckLocalId())
                            .dimension(check.getDimension())
                            .severity(severity)
                            .code("terminology-code-invalid")
                            .message(String.format("Code '%s' in system '%s' is not valid%s", code, system, detail))
                            .location(location(resource))
                            .expression(system + "|" + code)
                            .build());
                }
            }
        }
        log.info("TERMINOLOGY check '{}' done: {} coding(s) validated, {} invalid, {} skipped -> {} finding(s)",
                check.getCheckLocalId(), checked, invalid, skipped, findings.size());
        return findings;
    }

    private JsonNode parseParams(RubricCheck check) {
        if (check.getParametersJson() == null) return null;
        try {
            return objectMapper.readTree(check.getParametersJson());
        } catch (Exception e) {
            log.warn("TERMINOLOGY check {} has invalid parameters JSON: {}", check.getCheckLocalId(), e.getMessage());
            return null;
        }
    }

    private String location(IBaseResource resource) {
        String id = resource.getIdElement() != null ? resource.getIdElement().getIdPart() : null;
        return (id != null && !id.isBlank()) ? resource.fhirType() + "/" + id : resource.fhirType();
    }
}
