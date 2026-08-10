package com.lantanagroup.link.validation.services.execution.executors;

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
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.BooleanType;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.stream.Collectors;

@Component
@RequiredArgsConstructor
@Slf4j
public class FhirPathCheckExecutor implements CheckExecutor {

    private final IFhirPath fhirPath;
    private final ObjectMapper objectMapper;

    @Override
    public CheckType supports() {
        return CheckType.FHIRPATH;
    }

    @Override
    public List<RawFinding> execute(RubricCheck check, ExecutionContext context) {
        if (check.getParametersJson() == null) {
            log.warn("FHIRPATH check {} missing parameters", check.getCheckLocalId());
            return Collections.emptyList();
        }
        final JsonNode params;
        try {
            params = objectMapper.readTree(check.getParametersJson());
        } catch (Exception e) {
            log.error("FHIRPATH check {} has invalid parameters JSON", check.getCheckLocalId(), e);
            return Collections.emptyList();
        }

        String expression = params.path("expression").asText(null);
        if (expression == null || expression.isBlank()) {
            log.warn("FHIRPATH check {} missing expression", check.getCheckLocalId());
            return Collections.emptyList();
        }

        List<IBaseResource> targets = resolveTargets(context, expression);
        if (targets.isEmpty()) {
            return Collections.emptyList();
        }

        String failureMessage = params.path("failureMessage").asText("FHIRPath assertion failed: " + expression);
        String code = params.path("code").asText("fhirpath-assertion-failed");
        Severity severity = check.getSeverityOverride() != null ? check.getSeverityOverride() : Severity.ERROR;

        List<RawFinding> findings = new ArrayList<>();
        for (IBaseResource target : targets) {
            try {
                boolean passed = fhirPath.evaluateFirst(target, expression, BooleanType.class)
                        .map(BooleanType::booleanValue)
                        .orElse(false);
                if (!passed) {
                    findings.add(RawFinding.builder()
                            .checkLocalId(check.getCheckLocalId())
                            .dimension(check.getDimension())
                            .severity(severity)
                            .code(code)
                            .message(failureMessage)
                            .location(resourceLocation(target))
                            .expression(expression)
                            .build());
                }
            } catch (Exception e) {
                log.error("FHIRPATH evaluation failed for check {}: {}", check.getCheckLocalId(), e.getMessage());
                findings.add(RawFinding.builder()
                        .checkLocalId(check.getCheckLocalId())
                        .dimension(check.getDimension())
                        .severity(Severity.ERROR)
                        .code("fhirpath-evaluation-error")
                        .message("FHIRPath evaluation error: " + e.getMessage())
                        .location(resourceLocation(target))
                        .expression(expression)
                        .build());
            }
        }
        return findings;
    }

    private List<IBaseResource> resolveTargets(ExecutionContext context, String expression) {
        if (context.getBundleEntries().isEmpty()) {
            IBaseResource root = context.getResource();
            // an empty bundle and a bare single-resource payload both have no entries, but they
            // mean opposite things. don't run Patient.*/Observation.* checks against an empty
            // bundle's envelope — that just invents findings. a real Bundle.* expression still
            // evaluates against it (e.g. "Bundle.entry.count() >= 1" to flag an empty bundle).
            if (root != null && "Bundle".equals(root.fhirType())) {
                return "Bundle".equals(leadingResourceType(expression))
                        ? List.of(root)
                        : Collections.emptyList();
            }
            return List.of(root);
        }
        String resourceType = leadingResourceType(expression);
        if (resourceType == null) {
            return context.getBundleEntries();
        }
        // Bundle-level expression (e.g. "Bundle.entry.count() >= 1"): evaluate against the Bundle
        // itself rather than filtering entries, which would never match and silently pass. A Bundle
        // nested inside another Bundle's entries is not individually targeted here — acceptable.
        IBaseResource root = context.getResource();
        if (root != null && resourceType.equals(root.fhirType())) {
            return List.of(root);
        }
        return context.getBundleEntries().stream()
                .filter(e -> e.fhirType().equals(resourceType))
                .collect(Collectors.toList());
    }

    private String leadingResourceType(String expression) {
        int dot = expression.indexOf('.');
        if (dot <= 0) return null;
        String token = expression.substring(0, dot);
        return Character.isUpperCase(token.charAt(0)) ? token : null;
    }

    private String resourceLocation(IBaseResource resource) {
        String id = resource.getIdElement() != null ? resource.getIdElement().getIdPart() : null;
        return (id != null && !id.isBlank()) ? resource.fhirType() + "/" + id : resource.fhirType();
    }
}
