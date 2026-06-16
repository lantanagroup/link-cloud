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
 * <p>Constructor-time validation drops malformed rules with a warning rather than failing startup:
 * a category with {@code strategy=SKIP} but {@code acceptable=false} or empty/null scope is
 * silently demoted to LABEL behaviour (the rule simply doesn't appear in this advisor's list, so
 * the existing post-validation matcher still applies).</p>
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
            CategoryScope scope = category.getScope();
            if (scope == null || scope.getCodeSystems() == null || scope.getCodeSystems().isEmpty()) {
                // Other scope axes (valueSets / referencePaths) are not consumed by Phase 1.
                // A rule with no codeSystems patterns has nothing to fire on here; leave it to LABEL.
                continue;
            }
            List<Pattern> patterns = new ArrayList<>();
            for (String regex : scope.getCodeSystems()) {
                try {
                    patterns.add(Pattern.compile(regex));
                } catch (PatternSyntaxException e) {
                    logger.warn("Category '{}' has an invalid codeSystems regex: '{}' ({}); skipping that pattern.",
                            category.getId(), regex, e.getMessage());
                }
            }
            if (!patterns.isEmpty()) {
                result.add(new CompiledSkipRule(category.getId(), patterns));
            }
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
        if (systems != null && !systems.isEmpty() && !codeSystemSkipRules.isEmpty()) {
            for (CompiledSkipRule rule : codeSystemSkipRules) {
                for (String system : systems) {
                    if (rule.matchesSystem(system)) {
                        metrics.incrementRuleOutcome(rule.ruleId(), ValidationMetrics.OUTCOME_SKIPPED);
                        return EnumSet.noneOf(IValidationPolicyAdvisor.CodedContentValidationAction.class);
                    }
                }
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

    private record CompiledSkipRule(String ruleId, List<Pattern> codeSystemPatterns) {
        boolean matchesSystem(String system) {
            if (system == null) {
                return false;
            }
            for (Pattern p : codeSystemPatterns) {
                if (p.matcher(system).find()) {
                    return true;
                }
            }
            return false;
        }
    }
}
