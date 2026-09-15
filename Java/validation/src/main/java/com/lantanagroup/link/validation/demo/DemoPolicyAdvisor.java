package com.lantanagroup.link.validation.demo;

import org.hl7.fhir.common.hapi.validation.validator.FhirDefaultPolicyAdvisor;
import org.hl7.fhir.r5.model.ElementDefinition;
import org.hl7.fhir.r5.model.StructureDefinition;
import org.hl7.fhir.r5.utils.validation.IResourceValidator;
import org.hl7.fhir.r5.utils.validation.IValidationPolicyAdvisor;

import java.util.EnumSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;

/**
 * Minimal illustrative {@link IValidationPolicyAdvisor} that shows what
 * {@code policyForElement} gives us that {@code policyForCodedContent} does not.
 *
 * <p>Each {@link Rule} names one or more FHIR element paths (regex-matched against HAPI's
 * {@code path} argument) and the {@code ElementValidationAction} values to exclude for those
 * elements. HAPI asks this advisor once per element visit; whatever we exclude from the
 * returned {@link EnumSet} never runs. Excluding {@code Cardinality} silences "minimum
 * required = 1, but only found 0" for that element. Excluding {@code Invariants} skips
 * FHIRPath rules like {@code us-core-N}. Excluding {@code Bindings} short-circuits terminology
 * before it engages — one layer above {@code policyForCodedContent}.
 *
 * <p>Rule matching is first-hit-wins in the order supplied. Every fired rule increments a
 * counter accessible via {@link #getRuleHitCounts()} so the demo runner can attribute the
 * suppression back to specific rules.
 *
 * <p>Deliberately not a Spring bean, not backed by the {@code Category} entity, and not
 * cache-aware. This is a teaching artefact. The production shape (loading rules from the
 * category rule set with {@code acceptable=false} demotion, path-pattern compile guards,
 * OpenTelemetry attribution) is sketched in
 * {@code scratch/policy-for-element-opportunity.md}.
 */
public class DemoPolicyAdvisor extends FhirDefaultPolicyAdvisor {

    /**
     * Records one demo rule. Path patterns are matched against the FHIRPath-shaped
     * {@code path} argument HAPI passes to {@code policyForElement} (e.g.
     * {@code "Encounter.status"}); a rule matches when at least one of its patterns matches
     * the path.
     */
    public record Rule(String id, List<Pattern> pathPatterns,
                       EnumSet<IValidationPolicyAdvisor.ElementValidationAction> excludeActions) {

        public static Rule of(String id, String pathRegex,
                              IValidationPolicyAdvisor.ElementValidationAction... exclude) {
            return new Rule(id, List.of(Pattern.compile(pathRegex)), EnumSet.copyOf(List.of(exclude)));
        }

        boolean matches(String path) {
            if (path == null) return false;
            for (Pattern p : pathPatterns) {
                if (p.matcher(path).matches()) return true;
            }
            return false;
        }
    }

    private final List<Rule> rules;
    // LinkedHashMap preserves rule-declaration order for stable demo output.
    private final Map<String, Integer> ruleHitCounts = new LinkedHashMap<>();

    public DemoPolicyAdvisor(List<Rule> rules) {
        this.rules = List.copyOf(rules);
        for (Rule r : rules) {
            ruleHitCounts.put(r.id(), 0);
        }
    }

    @Override
    public EnumSet<IValidationPolicyAdvisor.ElementValidationAction> policyForElement(
            IResourceValidator validator, Object appContext,
            StructureDefinition structure, ElementDefinition definition, String path) {
        for (Rule rule : rules) {
            if (rule.matches(path)) {
                ruleHitCounts.merge(rule.id(), 1, Integer::sum);
                // Return the complement of the rule's exclude set — HAPI runs every action
                // that stays in the returned EnumSet and skips everything else.
                return EnumSet.complementOf(rule.excludeActions());
            }
        }
        return super.policyForElement(validator, appContext, structure, definition, path);
    }

    public Map<String, Integer> getRuleHitCounts() {
        return Map.copyOf(ruleHitCounts);
    }

    public List<Rule> getRules() {
        return rules;
    }
}
