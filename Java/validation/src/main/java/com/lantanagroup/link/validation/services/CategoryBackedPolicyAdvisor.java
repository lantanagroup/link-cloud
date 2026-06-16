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
import java.util.List;
import java.util.regex.Pattern;
import java.util.regex.PatternSyntaxException;

/**
 * HAPI {@code IValidationPolicyAdvisor} backed by the categorization rule set. Currently only
 * implements the {@code policyForCodedContent} hook — when a {@link CategoryStrategy#SKIP} rule's
 * scope matches an incoming code system URL, we return an empty action set so the validator
 * short-circuits all coded-content checks (no terminology lookup, no message produced). All other
 * advisor methods inherit from {@link FhirDefaultPolicyAdvisor} so HAPI's default policy decisions
 * apply unchanged.
 *
 * <p><b>Unscoped SKIP rules.</b> A SKIP rule with no {@code scope.codeSystems} narrowing fires on
 * every {@code policyForCodedContent} call, suppressing all coded-content validation for that
 * element. This is a coarse, high-risk semantic — returning an empty action set kills validation
 * for any other rule that would have caught a message at that element, including rules with
 * {@code acceptable=false}. The capability is left in place for genuinely-global suppression
 * cases that may come up later, but it should not be used to mean "I haven't figured out the
 * scope yet." Prefer a precise {@code scope.codeSystems} list, or stay LABEL until SUPPRESS
 * support exists.</p>
 *
 * <p><b>SUPPRESS gap (TODO).</b> Some categorization rules cover messages that can't be safely
 * targeted via {@code policyForCodedContent} (the four generic-shape branches of
 * {@code unknown_code_system}, for example: {@code ^Unknown Code System '.*'$},
 * {@code A code with no system...}, {@code ^CodeSystem is unknown...}, and the
 * {@code V3 ValueSet ActEncounterCode} variant). Properly handling those requires
 * {@code isSuppressMessageId(path, messageId)} matched against the stable HAPI message
 * identifiers in {@code org.hl7.fhir.utilities.i18n.I18nConstants}. Mapping each rule's matcher
 * branches to specific {@code I18nConstants} entries is research-heavy but bounded; see the
 * Phase 4 entry in {@code Java/validation/VALIDATION-CATEGORIES-DESIGN.md}.</p>
 *
 * <p>Constructor-time validation drops malformed rules with a warning rather than failing
 * startup: a category with {@code strategy=SKIP} but {@code acceptable=false} is demoted with a
 * warning (silently promoting a blocking rule to SKIP would mask failures). A SKIP rule whose
 * scope.codeSystems patterns all fail to compile is also demoted — falling back to "always skip"
 * semantics the author didn't ask for would over-skip.</p>
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

    public CategoryBackedPolicyAdvisor(CategoryRepository categoryRepository, ValidationMetrics metrics) {
        this.metrics = metrics;
        this.codeSystemSkipRules = loadCodeSystemSkipRules(categoryRepository);
        logger.info("CategoryBackedPolicyAdvisor loaded with {} code-system SKIP rule(s)",
                codeSystemSkipRules.size());
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
            result.add(new CompiledSkipRule(category.getId(), patterns));
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
                return EnumSet.noneOf(IValidationPolicyAdvisor.CodedContentValidationAction.class);
            }
        }
        return super.policyForCodedContent(
                validator, appContext, stackPath, definition, structure, kind, purpose, valueSet, systems);
    }

    /**
     * Test/diagnostic accessor. Not part of the runtime contract.
     */
    List<String> getLoadedSkipRuleIds() {
        return codeSystemSkipRules.stream().map(CompiledSkipRule::ruleId).toList();
    }

    /**
     * @param codeSystemPatterns {@code null} or empty means "always match" — the SKIP rule was
     *                           authored without a {@code scope.codeSystems} narrowing.
     */
    private record CompiledSkipRule(String ruleId, List<Pattern> codeSystemPatterns) {
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
    }
}
