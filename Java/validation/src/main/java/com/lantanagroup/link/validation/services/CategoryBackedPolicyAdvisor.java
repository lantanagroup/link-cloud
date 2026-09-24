package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategoryScope;
import com.lantanagroup.link.validation.entities.CategoryStrategy;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import org.hl7.fhir.common.hapi.validation.validator.FhirDefaultPolicyAdvisor;
import org.hl7.fhir.r5.model.ElementDefinition;
import org.hl7.fhir.r5.model.StructureDefinition;
import org.hl7.fhir.r5.model.ValueSet;
import org.hl7.fhir.r5.utils.validation.IResourceValidator;
import org.hl7.fhir.r5.utils.validation.IValidationPolicyAdvisor;
import org.hl7.fhir.r5.utils.validation.constants.BindingKind;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.context.annotation.Scope;
import org.springframework.context.annotation.ScopedProxyMode;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.EnumSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;
import java.util.regex.PatternSyntaxException;

/**
 * HAPI {@code IValidationPolicyAdvisor} backed by the categorization rule set. Implements two
 * hooks: {@code policyForCodedContent} (SKIP strategy — short-circuit terminology validation
 * before it runs) and {@code isSuppressMessageId} (SUPPRESS strategy — drop a specific message
 * by its stable {@code I18nConstants} ID after the check ran). All other advisor methods inherit
 * from {@link FhirDefaultPolicyAdvisor} so HAPI's default policy decisions apply unchanged.
 *
 * <p>The two strategies are independent and can coexist on a single rule. {@code unknown_code_system}
 * uses SKIP for the URL-specific matcher branches and SUPPRESS for the generic-shape branches
 * that can't be safely targeted via {@code policyForCodedContent}. The rule's {@code strategy}
 * field is a human-readable hint for the primary intent; the advisor wires whichever hooks are
 * configured by field presence (a non-null {@code scope} for SKIP, a non-empty
 * {@code suppressMessageIds} list for SUPPRESS).</p>
 *
 * <p><b>excludeActions surgical narrowing.</b> The default Phase 1 semantic for a matched SKIP
 * rule was "skip every coded-content check" ({@code EnumSet.noneOf(...)}), which is correct
 * when the team's intent for that code system URL is "don't validate anything." Phase 2 adds
 * {@code scope.excludeActions} so a rule can declare {@code ["InvalidDisplay"]} (for example)
 * and the advisor returns {@code EnumSet.complementOf(EnumSet.of(InvalidDisplay))} — every
 * check runs except the display-name check. Lets us migrate rules like
 * {@code incorrect_display_value_for_code} as unscoped SKIP without the over-skip hazard.</p>
 *
 * <p><b>Unscoped SKIP rules.</b> A SKIP rule with no {@code scope.codeSystems} narrowing fires on
 * every {@code policyForCodedContent} call. Without {@code excludeActions} this suppresses all
 * coded-content validation for that element — a coarse, high-risk semantic that kills validation
 * for any other rule that would have caught a message at that element, including rules with
 * {@code acceptable=false}. Unscoped SKIP is acceptable when paired with {@code excludeActions}
 * (surgical removal of one or two actions across every call) but should not be used without it
 * to mean "I haven't figured out the scope yet." Prefer precise {@code scope.codeSystems}, or
 * stay LABEL — or pair with a SUPPRESS rule that targets the same message family by ID.</p>
 *
 * <p><b>SUPPRESS via {@code isSuppressMessageId}.</b> A rule with a non-empty
 * {@code suppressMessageIds} list declares which stable HAPI message IDs (from
 * {@code org.hl7.fhir.utilities.i18n.I18nConstants}) the advisor should drop. The check still
 * runs — SUPPRESS saves no CPU — but the message is dropped before reaching the
 * {@code OperationOutcome}.</p>
 *
 * <p><b>Path narrowing.</b> A rule may also declare {@code suppressPathPatterns}: regex
 * patterns matched against the {@code path} argument HAPI passes to {@code isSuppressMessageId}.
 * When non-empty, the rule fires only when both its message ID matches AND at least one path
 * pattern matches the path. This lets us migrate rules whose matcher narrows by FHIR element
 * (e.g. {@code medicationrequest_requester_does_not_have_a_proper_reference} targets
 * {@code Reference_REF_NoDisplay} but only on {@code MedicationRequest.requester} paths) without
 * over-suppressing the message on every other element. When {@code suppressPathPatterns} is
 * null or empty, the rule fires on any path — Phase 4's default behaviour and the shape
 * {@code unknown_code_system} relies on.</p>
 *
 * <p>Multiple rules can register the same message ID. On a hook call we iterate the candidates
 * in repository order and the first whose {@code suppressPathPatterns} match wins counter
 * credit. The user-visible outcome (boolean suppress) is identical regardless of which rule
 * wins; only metric attribution differs.</p>
 *
 * <p>Constructor-time validation drops malformed rules with a warning rather than failing
 * startup: a category with {@code strategy=SKIP} but {@code acceptable=false} is demoted (silently
 * promoting a blocking rule to SKIP would mask failures); the same demotion applies to
 * SUPPRESS rules with {@code acceptable=false}. A SKIP rule whose scope.codeSystems patterns
 * all fail to compile is demoted — falling back to "always skip" semantics the author didn't
 * ask for would over-skip. Null or blank entries in {@code suppressMessageIds} are individually
 * dropped with a warning.</p>
 *
 * <p>Scoped {@code prototype} so each {@link ValidationService} instance gets a fresh rule
 * snapshot at injection time — categories can be updated via {@code initializeCategories()} and
 * the next request will see the new state without manual refresh.</p>
 */
@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class CategoryBackedPolicyAdvisor extends FhirDefaultPolicyAdvisor {
    private static final Logger logger = LoggerFactory.getLogger(CategoryBackedPolicyAdvisor.class);

    private final ValidationMetrics metrics;
    private final List<CompiledSkipRule> codeSystemSkipRules;
    /**
     * Map from suppressible HAPI message ID to one or more rules that claimed it. O(1) on the
     * message ID; rules are iterated in insertion order so first-match-wins is deterministic.
     */
    private final Map<String, List<CompiledSuppressRule>> suppressRulesByMessageId;

    public CategoryBackedPolicyAdvisor(CategoryRepository categoryRepository, ValidationMetrics metrics) {
        this.metrics = metrics;
        this.codeSystemSkipRules = loadCodeSystemSkipRules(categoryRepository);
        this.suppressRulesByMessageId = loadSuppressRules(categoryRepository);
        long suppressEntryCount = suppressRulesByMessageId.values().stream().mapToLong(List::size).sum();
        logger.info("CategoryBackedPolicyAdvisor loaded with {} code-system SKIP rule(s) and {} SUPPRESS registration(s) across {} distinct message ID(s)",
                codeSystemSkipRules.size(), suppressEntryCount, suppressRulesByMessageId.size());
    }

    private static List<CompiledSkipRule> loadCodeSystemSkipRules(CategoryRepository categoryRepository) {
        List<CompiledSkipRule> result = new ArrayList<>();
        for (Category category : categoryRepository.findAll()) {
            CategoryStrategy strategy = category.getStrategy();
            if (strategy != CategoryStrategy.SKIP) {
                continue;
            }
            if (!category.isAcceptable()) {
                logger.warn(
                        "Category '{}' has strategy=SKIP but acceptable=false; ignoring SKIP and falling back to LABEL behaviour. " +
                                "Promoting a blocking rule to SKIP would silently hide a failure; fix the data.",
                        category.getId());
                continue;
            }
            // A SKIP rule with no codeSystems scope means "always skip" at this hook — fire
            // for every policyForCodedContent call regardless of the systems argument. A rule
            // with a non-empty list narrows that to only systems matching at least one regex.
            CategoryScope scope = category.getScope();
            List<Pattern> patterns = null;
            if (scope != null && scope.getCodeSystems() != null && !scope.getCodeSystems().isEmpty()) {
                patterns = new ArrayList<>();
                for (String regex : scope.getCodeSystems()) {
                    try {
                        patterns.add(Pattern.compile(regex));
                    } catch (PatternSyntaxException e) {
                        logger.warn("Category '{}' has an invalid codeSystems regex: '{}' ({}); skipping that pattern.",
                                category.getId(), regex, e.getMessage());
                    }
                }
                if (patterns.isEmpty()) {
                    // All listed patterns were invalid; demote to LABEL rather than silently
                    // applying "always skip" semantics the author didn't ask for.
                    logger.warn("Category '{}' has codeSystems scope but every pattern failed to compile; demoting to LABEL.",
                            category.getId());
                    continue;
                }
            }
            // Resolve excludeActions enum names. Unknown names are logged and dropped. If the
            // author specified excludeActions but every name failed to resolve, demote — falling
            // back to "skip every action" semantics would be a behaviour change the author didn't
            // request.
            EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> excludeActions = null;
            if (scope != null && scope.getExcludeActions() != null && !scope.getExcludeActions().isEmpty()) {
                excludeActions = EnumSet.noneOf(IValidationPolicyAdvisor.CodedContentValidationAction.class);
                for (String name : scope.getExcludeActions()) {
                    try {
                        excludeActions.add(IValidationPolicyAdvisor.CodedContentValidationAction.valueOf(name));
                    } catch (IllegalArgumentException e) {
                        logger.warn("Category '{}' has an unknown excludeActions value: '{}'; ignoring that name.",
                                category.getId(), name);
                    }
                }
                if (excludeActions.isEmpty()) {
                    logger.warn("Category '{}' has excludeActions scope but every action name failed to resolve; demoting to LABEL.",
                            category.getId());
                    continue;
                }
            }
            result.add(new CompiledSkipRule(category.getId(), patterns, excludeActions));
        }
        return result;
    }

    @Override
    public EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> policyForCodedContent(
            IResourceValidator validator,
            Object appContext,
            String stackPath,
            ElementDefinition definition,
            StructureDefinition structure,
            BindingKind kind,
            IValidationPolicyAdvisor.AdditionalBindingPurpose purpose,
            ValueSet valueSet,
            List<String> systems) {
        for (CompiledSkipRule rule : codeSystemSkipRules) {
            if (rule.matchesAnyOf(systems)) {
                metrics.incrementRuleOutcome(rule.ruleId(), ValidationMetrics.OUTCOME_SKIPPED);
                return rule.resolveActionSet();
            }
        }
        return super.policyForCodedContent(
                validator, appContext, stackPath, definition, structure, kind, purpose, valueSet, systems);
    }

    @Override
    public boolean isSuppressMessageId(String path, String messageId) {
        if (messageId == null) {
            return false;
        }
        List<CompiledSuppressRule> candidates = suppressRulesByMessageId.get(messageId);
        if (candidates == null) {
            return false;
        }
        for (CompiledSuppressRule rule : candidates) {
            if (rule.matchesPath(path)) {
                metrics.incrementRuleOutcome(rule.ruleId(), ValidationMetrics.OUTCOME_SUPPRESSED);
                return true;
            }
        }
        return false;
    }

    private static Map<String, List<CompiledSuppressRule>> loadSuppressRules(CategoryRepository categoryRepository) {
        // LinkedHashMap to preserve insertion order — first-match-wins on hook calls becomes
        // deterministic, and test/log output is stable.
        Map<String, List<CompiledSuppressRule>> result = new LinkedHashMap<>();
        for (Category category : categoryRepository.findAll()) {
            List<String> messageIds = category.getSuppressMessageIds();
            if (messageIds == null || messageIds.isEmpty()) {
                continue;
            }
            if (!category.isAcceptable()) {
                logger.warn(
                        "Category '{}' has suppressMessageIds but acceptable=false; ignoring SUPPRESS and falling back to LABEL behaviour. " +
                                "Silently dropping a blocking message would mask failures; fix the data.",
                        category.getId());
                continue;
            }
            List<Pattern> pathPatterns = compilePathPatterns(category);
            CompiledSuppressRule compiled = new CompiledSuppressRule(category.getId(), pathPatterns);
            for (String id : messageIds) {
                if (id == null || id.isBlank()) {
                    logger.warn("Category '{}' has a null/blank entry in suppressMessageIds; ignoring.",
                            category.getId());
                    continue;
                }
                result.computeIfAbsent(id, k -> new ArrayList<>()).add(compiled);
            }
        }
        return result;
    }

    private static List<Pattern> compilePathPatterns(Category category) {
        List<String> raw = category.getSuppressPathPatterns();
        if (raw == null || raw.isEmpty()) {
            return null;
        }
        List<Pattern> patterns = new ArrayList<>();
        for (String regex : raw) {
            if (regex == null || regex.isBlank()) {
                logger.warn("Category '{}' has a null/blank entry in suppressPathPatterns; ignoring.",
                        category.getId());
                continue;
            }
            try {
                patterns.add(Pattern.compile(regex));
            } catch (PatternSyntaxException e) {
                logger.warn("Category '{}' has an invalid suppressPathPatterns regex: '{}' ({}); skipping that pattern.",
                        category.getId(), regex, e.getMessage());
            }
        }
        if (patterns.isEmpty()) {
            // All patterns were invalid — fall back to "match any path" semantics would silently
            // change scope from narrow to global. Demote by treating as if no path narrowing was
            // configured but log loudly so the misconfiguration is visible.
            logger.warn("Category '{}' has suppressPathPatterns but every pattern failed to compile; SUPPRESS will be skipped for this rule (it would otherwise widen to all paths, which the author did not request).",
                    category.getId());
            return List.of();  // sentinel: empty list means "rule loaded but never matches" — see CompiledSuppressRule.matchesPath
        }
        return patterns;
    }

    /**
     * Test/diagnostic accessor. Not part of the runtime contract.
     */
    List<String> getLoadedSkipRuleIds() {
        return codeSystemSkipRules.stream().map(CompiledSkipRule::ruleId).toList();
    }

    /**
     * Test/diagnostic accessor — returns a snapshot of {@code messageId → list of rule IDs that
     * registered it} so tests can verify load behaviour without exposing the compiled
     * {@code CompiledSuppressRule}s. Not part of the runtime contract.
     */
    Map<String, List<String>> getLoadedSuppressMap() {
        Map<String, List<String>> snapshot = new LinkedHashMap<>();
        for (Map.Entry<String, List<CompiledSuppressRule>> entry : suppressRulesByMessageId.entrySet()) {
            snapshot.put(entry.getKey(),
                    entry.getValue().stream().map(CompiledSuppressRule::ruleId).toList());
        }
        return snapshot;
    }

    /**
     * @param pathPatterns {@code null} means "no path narrowing — rule fires on any path"
     *                     (Phase 4 default for rules without {@code suppressPathPatterns}).
     *                     A non-null but empty list is the post-failure-demotion sentinel: the
     *                     rule was authored with path patterns but every one failed to compile,
     *                     so we register the rule but it never matches (treating it as if the
     *                     SUPPRESS hook was uninstalled — preserves the load count but doesn't
     *                     widen scope to all paths).
     */
    private record CompiledSuppressRule(String ruleId, List<Pattern> pathPatterns) {
        boolean matchesPath(String path) {
            if (pathPatterns == null) {
                return true;
            }
            if (pathPatterns.isEmpty() || path == null) {
                return false;
            }
            for (Pattern p : pathPatterns) {
                if (p.matcher(path).find()) {
                    return true;
                }
            }
            return false;
        }
    }

    /**
     * @param codeSystemPatterns {@code null} or empty means "always match" — the SKIP rule was
     *                           authored without a {@code scope.codeSystems} narrowing.
     * @param excludeActions     {@code null} or empty means "skip every action" ({@code noneOf}).
     *                           Non-empty means "skip only these actions" ({@code complementOf}).
     */
    private record CompiledSkipRule(
            String ruleId,
            List<Pattern> codeSystemPatterns,
            EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> excludeActions) {
        boolean matchesAnyOf(List<String> systems) {
            if (codeSystemPatterns == null || codeSystemPatterns.isEmpty()) {
                // Unscoped SKIP rule — fires on any policyForCodedContent call regardless of the
                // systems argument (which can be null/empty, e.g. when validating a coding with
                // no system specified). The team's intent for these rules is "I don't care about
                // terminology validation for these messages, period."
                return true;
            }
            if (systems == null || systems.isEmpty()) {
                return false;
            }
            for (String system : systems) {
                if (system == null) {
                    continue;
                }
                for (Pattern p : codeSystemPatterns) {
                    if (p.matcher(system).find()) {
                        return true;
                    }
                }
            }
            return false;
        }

        EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> resolveActionSet() {
            if (excludeActions == null || excludeActions.isEmpty()) {
                // Default Phase 1 behaviour: remove every check.
                return EnumSet.noneOf(IValidationPolicyAdvisor.CodedContentValidationAction.class);
            }
            // Surgical narrowing: every check runs except the named ones.
            return EnumSet.complementOf(excludeActions);
        }
    }
}
