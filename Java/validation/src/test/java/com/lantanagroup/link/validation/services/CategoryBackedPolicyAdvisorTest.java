package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategoryScope;
import com.lantanagroup.link.validation.entities.CategorySeverity;
import com.lantanagroup.link.validation.entities.CategoryStrategy;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import org.hl7.fhir.r5.utils.validation.IValidationPolicyAdvisor;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.EnumSet;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class CategoryBackedPolicyAdvisorTest {

    private CategoryRepository categoryRepository;
    private ValidationMetrics metrics;

    @BeforeEach
    void setUp() {
        categoryRepository = mock(CategoryRepository.class);
        metrics = mock(ValidationMetrics.class);
    }

    private static Category skipRule(String id, boolean acceptable, List<String> codeSystems) {
        Category c = new Category();
        c.setId(id);
        c.setTitle(id);
        c.setSeverity(CategorySeverity.ERROR);
        c.setAcceptable(acceptable);
        c.setGuidance("test");
        c.setStrategy(CategoryStrategy.SKIP);
        CategoryScope scope = new CategoryScope();
        scope.setCodeSystems(codeSystems);
        c.setScope(scope);
        return c;
    }

    private static Category labelRule(String id) {
        Category c = new Category();
        c.setId(id);
        c.setTitle(id);
        c.setSeverity(CategorySeverity.ERROR);
        c.setAcceptable(false);
        c.setGuidance("test");
        c.setStrategy(CategoryStrategy.LABEL);
        return c;
    }

    @Test
    void emptyRepository_loadsZeroSkipRules() {
        when(categoryRepository.findAll()).thenReturn(List.of());
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);
        assertTrue(advisor.getLoadedSkipRuleIds().isEmpty());
    }

    @Test
    void onlyLabelRules_loadsZeroSkipRules() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                labelRule("rule_a"), labelRule("rule_b")));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);
        assertTrue(advisor.getLoadedSkipRuleIds().isEmpty());
    }

    @Test
    void validSkipRule_isLoaded() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                skipRule("epic_codes", true, List.of("https?://open\\.epic\\.com/.*"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);
        assertEquals(List.of("epic_codes"), advisor.getLoadedSkipRuleIds());
    }

    @Test
    void skipRuleWithAcceptableFalse_isDemoted() {
        // A SKIP rule on a blocking (acceptable=false) category would silently hide failures.
        // Defensive: drop it from the advisor's list and warn.
        when(categoryRepository.findAll()).thenReturn(List.of(
                skipRule("blocking", false, List.of("https?://anything/.*"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);
        assertTrue(advisor.getLoadedSkipRuleIds().isEmpty());
    }

    @Test
    void skipRuleWithNoScope_isLoaded() {
        // "no scope" is a valid SKIP rule shape — it means "always skip" at this hook.
        Category nullScope = skipRule("always_skip", true, null);
        nullScope.setScope(null);

        when(categoryRepository.findAll()).thenReturn(List.of(nullScope));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);
        assertEquals(List.of("always_skip"), advisor.getLoadedSkipRuleIds());
    }

    @Test
    void skipRuleWithAllInvalidRegex_isDemoted() {
        // If every pattern in the scope failed to compile, falling back to "always skip" would
        // silently apply semantics the author didn't ask for. Better to demote and log loudly.
        when(categoryRepository.findAll()).thenReturn(List.of(
                skipRule("all_invalid", true, List.of("[unterminated", "(also-unterminated"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);
        assertTrue(advisor.getLoadedSkipRuleIds().isEmpty());
    }

    @Test
    void skipRuleWithInvalidRegex_otherPatternsStillLoad() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                skipRule("mixed", true, List.of("[unterminated", "https?://open\\.epic\\.com/.*"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);
        assertEquals(List.of("mixed"), advisor.getLoadedSkipRuleIds(),
                "Rule with at least one valid pattern should still be loaded");
    }

    @Test
    void policyForCodedContent_matchingSystem_returnsEmptyActionSet() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                skipRule("epic_codes", true, List.of("https?://open\\.epic\\.com/.*"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> result = advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("https://open.epic.com/FHIR/SomeCodeSystem"));

        assertTrue(result.isEmpty(), "Matching SKIP rule must return an empty action set (no checks performed)");
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("epic_codes"), eq(ValidationMetrics.OUTCOME_SKIPPED));
    }

    @Test
    void policyForCodedContent_nonMatchingSystem_delegatesToSuper() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                skipRule("epic_codes", true, List.of("https?://open\\.epic\\.com/.*"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> result = advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("http://loinc.org"));

        // FhirDefaultPolicyAdvisor returns the full action set by default. The exact set isn't part
        // of OUR contract (it's HAPI's), but we can assert that we DIDN'T short-circuit: a non-empty
        // result and no counter call confirm the default delegation took effect.
        assertFalse(result.isEmpty(),
                "Non-matching system should fall through to the default advisor which returns a non-empty action set");
        verify(metrics, never()).incrementRuleOutcome(anyString(), eq(ValidationMetrics.OUTCOME_SKIPPED));
    }

    @Test
    void policyForCodedContent_emptySystemsList_delegatesToSuper() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                skipRule("epic_codes", true, List.of("https?://open\\.epic\\.com/.*"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null, List.of());
        advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null, null);

        verify(metrics, never()).incrementRuleOutcome(anyString(), eq(ValidationMetrics.OUTCOME_SKIPPED));
    }

    @Test
    void policyForCodedContent_unscopedSkipRule_firesOnAnySystem() {
        Category unscoped = skipRule("always_skip", true, null);
        unscoped.setScope(null);
        when(categoryRepository.findAll()).thenReturn(List.of(unscoped));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> result = advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("http://loinc.org"));

        assertTrue(result.isEmpty(), "Unscoped SKIP rule must fire on any system");
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("always_skip"), eq(ValidationMetrics.OUTCOME_SKIPPED));
    }

    @Test
    void policyForCodedContent_unscopedSkipRule_firesEvenWhenSystemsEmpty() {
        // A coded element with no system specified still triggers policyForCodedContent with an
        // empty/null systems list. Unscoped SKIP rules cover that case — the team's intent for
        // "I don't care about unknown systems" includes "I don't care when there's no system."
        Category unscoped = skipRule("always_skip", true, null);
        unscoped.setScope(null);
        when(categoryRepository.findAll()).thenReturn(List.of(unscoped));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null, List.of());
        advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null, null);

        verify(metrics, times(2))
                .incrementRuleOutcome(eq("always_skip"), eq(ValidationMetrics.OUTCOME_SKIPPED));
    }

    @Test
    void policyForCodedContent_firstMatchingRuleWins() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                skipRule("rule_a", true, List.of("https?://open\\.epic\\.com/.*")),
                skipRule("rule_b", true, List.of("https?://open\\.epic\\.com/.*"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("https://open.epic.com/FHIR/X"));

        // Exactly one rule should be credited even when two match — the first matching rule wins
        // since we return immediately on the first hit.
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("rule_a"), eq(ValidationMetrics.OUTCOME_SKIPPED));
        verify(metrics, never())
                .incrementRuleOutcome(eq("rule_b"), eq(ValidationMetrics.OUTCOME_SKIPPED));
    }
}
