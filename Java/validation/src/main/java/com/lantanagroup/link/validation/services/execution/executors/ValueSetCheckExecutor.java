package com.lantanagroup.link.validation.services.execution.executors;

import ca.uhn.fhir.context.support.ConceptValidationOptions;
import ca.uhn.fhir.context.support.IValidationSupport;
import ca.uhn.fhir.context.support.ValidationSupportContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.execution.CheckExecutor;
import com.lantanagroup.link.validation.services.execution.CheckExecutorRegistry;
import com.lantanagroup.link.validation.services.execution.UnresolvedBindingClassifier;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.instance.model.api.IBase;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.instance.model.api.IPrimitiveType;
import org.hl7.fhir.r4.model.CodeableConcept;
import org.hl7.fhir.r4.model.Coding;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.stream.Collectors;

/**
 * VALUESET carries two modes in the same check type. Direct mode (path + valueSet) does the
 * original FHIRPath-then-membership check. Combinator mode (parameters.checks + matchMode) instead
 * combines several child checks ΓÇö of any type, VALUESET included ΓÇö into a single logical OR
 * (matchMode ANY) or AND (matchMode ALL), dispatching each child back through the same
 * {@link CheckExecutorRegistry}. This is how "or"/"and" across checks (e.g. a VALUESET branch that
 * may be unresolvable, OR'd with a literal-code FHIRPATH workaround) becomes expressible in a
 * rubric without a dedicated combinator check type.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class ValueSetCheckExecutor implements CheckExecutor {

    private final IFhirPath fhirPath;
    private final ValidationSupportChain validationSupportChain;
    private final ObjectMapper objectMapper;
    // ObjectProvider breaks the cycle: registry <- executors (including this one) <- registry
    private final ObjectProvider<CheckExecutorRegistry> registryProvider;

    private enum MatchMode { ANY, ALL }

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
        JsonNode checksNode = params.path("checks");
        if (checksNode.isArray() && !checksNode.isEmpty()) {
            return executeCombinator(check, context, params, checksNode);
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
        int notEvaluated = 0;
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
                    } else if (UnresolvedBindingClassifier.isUnresolvable(result) && !validationSupportChain.isCodeSystemSupported(supportContext, system)) {
                        // The value set (or the code system behind it) could not be resolved, so
                        // membership was never actually tested (HAPI issue coding NOT_FOUND, or a
                        // resolution-failure message). Emit a "not evaluated" finding (INCONCLUSIVE,
                        // ignored by scoring) instead of a false membership error.
                        notEvaluated++;
                        log.info("VALUESET check '{}': value set {} could not be resolved for code {}|{} -> NOT EVALUATED{}",
                                check.getCheckLocalId(), valueSet, system, code,
                                result.getMessage() != null ? " (" + result.getMessage() + ")" : "");
                        findings.add(RawFinding.builder()
                                .checkLocalId(check.getCheckLocalId())
                                .dimension(check.getDimension())
                                .severity(Severity.INFORMATION)
                                .notEvaluated(true)
                                .code("binding-not-evaluated")
                                .message(String.format("Value set %s could not be resolved; membership of code '%s'%s was not evaluated",
                                        valueSet, code, system != null ? " (" + system + ")" : ""))
                                .location(path)
                                .expression(path)
                                .build());
                    } else {
                        notMember++;
                        log.info("VALUESET check '{}': code {}|{} -> NOT a member of {}{}",
                                check.getCheckLocalId(), system, code, valueSet,
                                result.getMessage() != null ? " (" + result.getMessage() + ")" : "");
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
        log.info("VALUESET check '{}' done: {} code(s) checked against {}, {} not-a-member, {} not-evaluated, {} skipped -> {} finding(s)",
                check.getCheckLocalId(), checked, valueSet, notMember, notEvaluated, skipped, findings.size());
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

    private List<RawFinding> executeCombinator(RubricCheck check, ExecutionContext context, JsonNode params, JsonNode checksNode) {
        MatchMode matchMode = parseMatchMode(params, check);
        String failureMessage = params.path("failureMessage").asText(
                "None of the branches were satisfied for check " + check.getCheckLocalId());
        String code = params.path("code").asText(check.getCheckLocalId());
        Severity severity = check.getSeverityOverride() != null ? check.getSeverityOverride() : Severity.ERROR;

        CheckExecutorRegistry registry = registryProvider.getObject();

        List<RawFinding> reasons = new ArrayList<>();
        boolean anyPassed = false;
        boolean anyInconclusive = false;

        for (JsonNode childNode : checksNode) {
            RubricCheck synthetic = toSyntheticCheck(check, childNode);
            if (synthetic == null) {
                continue;
            }

            List<RawFinding> childFindings;
            try {
                childFindings = registry.get(synthetic.getType()).execute(synthetic, context);
            } catch (Exception e) {
                log.error("VALUESET (combinator) check '{}': child '{}' failed during execution",
                        check.getCheckLocalId(), synthetic.getCheckLocalId(), e);
                anyInconclusive = true;
                continue;
            }

            if (childFindings.isEmpty()) {
                log.info("VALUESET (combinator) check '{}': branch '{}' passed", check.getCheckLocalId(), synthetic.getCheckLocalId());
                anyPassed = true;
                if (matchMode == MatchMode.ANY) {
                    break; // short-circuit: one satisfied branch is enough for ANY
                }
                continue;
            }

            if (childFindings.stream().allMatch(RawFinding::isNotEvaluated)) {
                log.info("VALUESET (combinator) check '{}': branch '{}' could not be evaluated", check.getCheckLocalId(), synthetic.getCheckLocalId());
                anyInconclusive = true;
                continue;
            }

            log.info("VALUESET (combinator) check '{}': branch '{}' failed", check.getCheckLocalId(), synthetic.getCheckLocalId());
            reasons.addAll(childFindings);
        }

        if (matchMode == MatchMode.ANY && anyPassed) {
            return List.of();
        }
        if (matchMode == MatchMode.ALL && reasons.isEmpty()) {
            return anyInconclusive
                    ? List.of(notEvaluatedFinding(check, "Not every VALUESET (ALL) branch could be evaluated (unresolved value sets or execution errors)"))
                    : List.of();
        }
        if (matchMode == MatchMode.ANY && reasons.isEmpty()) {
            return anyInconclusive
                    ? List.of(notEvaluatedFinding(check, "No VALUESET (ANY) branch could be evaluated (unresolved value sets or execution errors)"))
                    : List.of();
        }

        return List.of(RawFinding.builder()
                .checkLocalId(check.getCheckLocalId())
                .dimension(check.getDimension())
                .severity(severity)
                .code(code)
                .message(failureMessage + summarise(reasons))
                .location(reasons.get(0).getLocation())
                .expression(reasons.get(0).getExpression())
                .build());
    }

    private RawFinding notEvaluatedFinding(RubricCheck check, String message) {
        return RawFinding.builder()
                .checkLocalId(check.getCheckLocalId())
                .dimension(check.getDimension())
                .severity(Severity.INFORMATION)
                .notEvaluated(true)
                .code("valueset-combinator-not-evaluated")
                .message(message)
                .build();
    }

    private String summarise(List<RawFinding> reasons) {
        return " (" + reasons.stream()
                .map(RawFinding::getMessage)
                .filter(m -> m != null && !m.isBlank())
                .distinct()
                .limit(10)
                .collect(Collectors.joining("; ")) + ")";
    }

    private MatchMode parseMatchMode(JsonNode params, RubricCheck check) {
        String raw = params.path("matchMode").asText("ANY");
        try {
            return MatchMode.valueOf(raw.trim().toUpperCase());
        } catch (IllegalArgumentException e) {
            log.warn("VALUESET (combinator) check {} has invalid matchMode '{}', defaulting to ANY", check.getCheckLocalId(), raw);
            return MatchMode.ANY;
        }
    }

    /**
     * Builds a transient (not persisted) RubricCheck for a child check definition, inheriting the
     * parent's dimension/severityOverride unless the child explicitly overrides them.
     */
    private RubricCheck toSyntheticCheck(RubricCheck parent, JsonNode childNode) {
        String typeText = childNode.path("type").asText(null);
        if (typeText == null || typeText.isBlank()) {
            log.warn("VALUESET (combinator) check {}: child check missing 'type', skipping", parent.getCheckLocalId());
            return null;
        }
        CheckType childType;
        try {
            childType = CheckType.valueOf(typeText.trim().toUpperCase());
        } catch (IllegalArgumentException e) {
            log.warn("VALUESET (combinator) check {}: child has unknown type '{}', skipping", parent.getCheckLocalId(), typeText);
            return null;
        }

        String childId = childNode.path("id").asText(null);
        String checkLocalId = parent.getCheckLocalId() + (childId != null && !childId.isBlank() ? "." + childId : "");

        PiqiDimension dimension = parent.getDimension();
        if (childNode.hasNonNull("dimension")) {
            try {
                dimension = PiqiDimension.valueOf(childNode.get("dimension").asText().trim().toUpperCase());
            } catch (IllegalArgumentException e) {
                log.warn("VALUESET (combinator) check {}: child '{}' has unknown dimension override, using parent's",
                        parent.getCheckLocalId(), checkLocalId);
            }
        }

        Severity severityOverride = parent.getSeverityOverride();
        if (childNode.hasNonNull("severityOverride")) {
            try {
                severityOverride = Severity.valueOf(childNode.get("severityOverride").asText().trim().toUpperCase());
            } catch (IllegalArgumentException e) {
                log.warn("VALUESET (combinator) check {}: child '{}' has unknown severityOverride, using parent's",
                        parent.getCheckLocalId(), checkLocalId);
            }
        }

        String parametersJson = null;
        if (childNode.has("parameters")) {
            try {
                parametersJson = objectMapper.writeValueAsString(childNode.get("parameters"));
            } catch (Exception e) {
                log.warn("VALUESET (combinator) check {}: child '{}' has unwritable parameters", parent.getCheckLocalId(), checkLocalId, e);
            }
        }

        return RubricCheck.builder()
                .checkLocalId(checkLocalId)
                .type(childType)
                .dimension(dimension)
                .severityOverride(severityOverride)
                .parametersJson(parametersJson)
                .enabled(true)
                .build();
    }
}
