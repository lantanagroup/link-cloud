package com.lantanagroup.link.validation.services.execution.executors;

import ca.uhn.fhir.context.support.ConceptValidationOptions;
import ca.uhn.fhir.context.support.IValidationSupport;
import ca.uhn.fhir.context.support.ValidationSupportContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
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
import org.hl7.fhir.instance.model.api.IBase;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.instance.model.api.IPrimitiveType;
import org.hl7.fhir.r4.model.CodeableConcept;
import org.hl7.fhir.r4.model.Coding;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

@Component
@RequiredArgsConstructor
@Slf4j
public class ValueSetCheckExecutor implements CheckExecutor {

    private final IFhirPath fhirPath;
    private final ValidationSupportChain validationSupportChain;
    private final ObjectMapper objectMapper;

    @Override
    public CheckType supports() {
        return CheckType.VALUESET;
    }

    @Override
    public List<RawFinding> execute(RubricCheck check, ExecutionContext context) {
        JsonNode params = parseParams(check);
        if (params == null) {
            log.warn("VALUESET check {} missing parameters", check.getCheckLocalId());
            return List.of();
        }
        String path = params.path("path").asText(null);
        String valueSet = params.path("valueSet").asText(null);
        String fallbackSystem = params.path("system").asText(null);
        if (path == null || path.isBlank() || valueSet == null || valueSet.isBlank()) {
            log.warn("VALUESET check {} requires both 'path' and 'valueSet'", check.getCheckLocalId());
            return List.of();
        }

        Severity severity = check.getSeverityOverride() != null ? check.getSeverityOverride() : Severity.ERROR;
        ValidationSupportContext supportContext = new ValidationSupportContext(validationSupportChain);
        ConceptValidationOptions options = new ConceptValidationOptions().setInferSystem(true);

        List<IBaseResource> targets = context.getBundleEntries().isEmpty()
                ? List.of(context.getResource())
                : context.getBundleEntries();

        log.info("VALUESET check '{}': evaluating FHIRPath '{}' on {} resource(s), then checking membership in value set {} via the ValidationSupportChain",
                check.getCheckLocalId(), path, targets.size(), valueSet);

        int checked = 0;
        int notMember = 0;
        int skipped = 0;
        List<RawFinding> findings = new ArrayList<>();
        for (IBaseResource resource : targets) {
            List<IBase> nodes;
            try {
                nodes = fhirPath.evaluate(resource, path, IBase.class);
            } catch (Exception e) {
                log.debug("VALUESET path '{}' did not evaluate on {}: {}", path, resource.fhirType(), e.getMessage());
                continue;
            }
            if (!nodes.isEmpty()) {
                log.info("VALUESET check '{}': FHIRPath '{}' matched {} node(s) on {}",
                        check.getCheckLocalId(), path, nodes.size(), resource.fhirType());
            }
            for (IBase node : nodes) {
                for (String[] sc : extractCodes(node, fallbackSystem)) {
                    String system = sc[0];
                    String code = sc[1];
                    String display = sc[2];
                    if (code == null || code.isBlank()) continue;
                    checked++;
                    IValidationSupport.CodeValidationResult result;
                    try {
                        result = validationSupportChain.validateCode(supportContext, options, system, code, display, valueSet);
                    } catch (Exception e) {
                        log.info("VALUESET check '{}': validateCode threw for {}|{} in {} — skipping this code: {}",
                                check.getCheckLocalId(), system, code, valueSet, e.getMessage());
                        skipped++;
                        continue;
                    }
                    if (result == null) {
                        log.info("VALUESET check '{}': no validation support could answer for code {}|{} against {} — SKIPPED (counts as pass)",
                                check.getCheckLocalId(), system, code, valueSet);
                        skipped++;
                        continue;
                    }
                    if (result.isOk()) {
                        log.info("VALUESET check '{}': code {}|{} -> IS a member of {}",
                                check.getCheckLocalId(), system, code, valueSet);
                    } else {
                        notMember++;
                        log.info("VALUESET check '{}': code {}|{} -> NOT a member of {}{}",
                                check.getCheckLocalId(), system, code, valueSet,
                                result.getMessage() != null ? " (" + result.getMessage() + ")" : "");
                    }
                    if (!result.isOk()) {
                        findings.add(RawFinding.builder()
                                .checkLocalId(check.getCheckLocalId())
                                .dimension(check.getDimension())
                                .severity(severity)
                                .code("valueset-membership-failed")
                                .message(String.format("Code '%s'%s is not in value set %s",
                                        code, system != null ? " (" + system + ")" : "", valueSet))
                                .location(path)
                                .expression(path)
                                .build());
                    }
                }
            }
        }
        log.info("VALUESET check '{}' done: {} code(s) checked against {}, {} not-a-member, {} skipped -> {} finding(s)",
                check.getCheckLocalId(), checked, valueSet, notMember, skipped, findings.size());
        return findings;
    }

    private List<String[]> extractCodes(IBase node, String fallbackSystem) {
        if (node instanceof Coding coding) {
            return List.<String[]>of(new String[]{coding.getSystem(), coding.getCode(), coding.getDisplay()});
        }
        if (node instanceof CodeableConcept cc) {
            List<String[]> out = new ArrayList<>();
            for (Coding coding : cc.getCoding()) {
                out.add(new String[]{coding.getSystem(), coding.getCode(), coding.getDisplay()});
            }
            return out;
        }
        if (node instanceof IPrimitiveType<?> primitive) {
            return List.<String[]>of(new String[]{fallbackSystem, primitive.getValueAsString(), null});
        }
        return Collections.emptyList();
    }

    private JsonNode parseParams(RubricCheck check) {
        if (check.getParametersJson() == null) return null;
        try {
            return objectMapper.readTree(check.getParametersJson());
        } catch (Exception e) {
            log.warn("VALUESET check {} has invalid parameters JSON: {}", check.getCheckLocalId(), e.getMessage());
            return null;
        }
    }
}
