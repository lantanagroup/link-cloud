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

    // --- excludeActions (Phase 2) -------------------------------------------------------------

    private static Category excludeActionsRule(String id, List<String> codeSystems, List<String> excludeActions) {
        Category c = skipRule(id, true, codeSystems);
        if (c.getScope() == null) {
            c.setScope(new CategoryScope());
        }
        c.getScope().setExcludeActions(excludeActions);
        return c;
    }

    @Test
    void excludeActions_ruleWithoutExcludeActions_returnsNoneOf() {
        // Phase 1 default: a matched SKIP rule with no excludeActions skips every check.
        when(categoryRepository.findAll()).thenReturn(List.of(
                skipRule("epic_codes", true, List.of("https?://open\\.epic\\.com/.*"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> result = advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("https://open.epic.com/FHIR/X"));

        assertTrue(result.isEmpty(),
                "Without excludeActions a matched rule must keep returning the empty set");
    }

    @Test
    void excludeActions_singleAction_returnsComplementOf() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                excludeActionsRule("display_only", null, List.of("InvalidDisplay"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> result = advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("http://loinc.org"));

        assertEquals(
                EnumSet.complementOf(EnumSet.of(IValidationPolicyAdvisor.CodedContentValidationAction.InvalidDisplay)),
                result,
                "Excluding InvalidDisplay must leave every other action in the returned set");
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("display_only"), eq(ValidationMetrics.OUTCOME_SKIPPED));
    }

    @Test
    void excludeActions_multipleActions_returnsComplementOfAll() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                excludeActionsRule("display_and_code", null, List.of("InvalidDisplay", "InvalidCode"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> result = advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("http://loinc.org"));

        assertEquals(
                EnumSet.complementOf(EnumSet.of(
                        IValidationPolicyAdvisor.CodedContentValidationAction.InvalidDisplay,
                        IValidationPolicyAdvisor.CodedContentValidationAction.InvalidCode)),
                result);
    }

    @Test
    void excludeActions_unknownActionName_isDroppedRuleStillLoadsIfAnyValid() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                excludeActionsRule("mixed", null, List.of("NotARealAction", "InvalidDisplay"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertEquals(List.of("mixed"), advisor.getLoadedSkipRuleIds(),
                "Rule with at least one valid action should still be loaded");

        EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> result = advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("http://loinc.org"));

        assertEquals(
                EnumSet.complementOf(EnumSet.of(IValidationPolicyAdvisor.CodedContentValidationAction.InvalidDisplay)),
                result,
                "Only the valid action name should affect the returned set");
    }

    @Test
    void excludeActions_allInvalidActionNames_demotesRule() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                excludeActionsRule("all_bad", null, List.of("NotARealAction", "AlsoNotReal"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertTrue(advisor.getLoadedSkipRuleIds().isEmpty(),
                "If every action name fails to resolve, the rule must be demoted to LABEL — " +
                        "silently reverting to 'skip every action' would change semantics the author didn't request");
    }

    @Test
    void excludeActions_combinedWithCodeSystemsScope() {
        // The two axes compose: codeSystems narrows WHEN the rule fires, excludeActions narrows
        // WHICH actions are removed when it does.
        when(categoryRepository.findAll()).thenReturn(List.of(
                excludeActionsRule("epic_display_only",
                        List.of("https?://open\\.epic\\.com/.*"),
                        List.of("InvalidDisplay"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        // Matching system → complementOf({InvalidDisplay})
        EnumSet<IValidationPolicyAdvisor.CodedContentValidationAction> matched = advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("https://open.epic.com/FHIR/X"));
        assertEquals(
                EnumSet.complementOf(EnumSet.of(IValidationPolicyAdvisor.CodedContentValidationAction.InvalidDisplay)),
                matched);
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("epic_display_only"), eq(ValidationMetrics.OUTCOME_SKIPPED));

        // Non-matching system → delegate to super, counter NOT incremented again
        advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("http://loinc.org"));
        verify(metrics, times(1))  // still 1
                .incrementRuleOutcome(eq("epic_display_only"), eq(ValidationMetrics.OUTCOME_SKIPPED));
    }

    // --- isSuppressMessageId (Phase 4) ---------------------------------------------------------

    private static Category suppressRule(String id, boolean acceptable, List<String> messageIds) {
        Category c = new Category();
        c.setId(id);
        c.setTitle(id);
        c.setSeverity(CategorySeverity.ERROR);
        c.setAcceptable(acceptable);
        c.setGuidance("test");
        c.setStrategy(CategoryStrategy.SUPPRESS);
        c.setSuppressMessageIds(messageIds);
        return c;
    }

    @Test
    void isSuppressMessageId_noRules_returnsFalse() {
        when(categoryRepository.findAll()).thenReturn(List.of());
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertFalse(advisor.isSuppressMessageId("Patient.gender", "Terminology_TX_System_Unknown"));
        verify(metrics, never()).incrementRuleOutcome(anyString(), anyString());
    }

    @Test
    void isSuppressMessageId_matchingId_returnsTrueAndIncrementsCounter() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRule("unknown_cs_generic", true, List.of("Terminology_TX_System_Unknown"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertTrue(advisor.isSuppressMessageId(
                "Patient.identifier.system", "Terminology_TX_System_Unknown"));
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("unknown_cs_generic"), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
    }

    @Test
    void isSuppressMessageId_nonMatchingId_returnsFalse() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRule("unknown_cs_generic", true, List.of("Terminology_TX_System_Unknown"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertFalse(advisor.isSuppressMessageId("Patient.gender", "Some_Other_Message_Id"));
        verify(metrics, never())
                .incrementRuleOutcome(anyString(), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
    }

    @Test
    void isSuppressMessageId_nullMessageId_returnsFalse() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRule("unknown_cs_generic", true, List.of("Terminology_TX_System_Unknown"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertFalse(advisor.isSuppressMessageId("Patient.gender", null));
        verify(metrics, never()).incrementRuleOutcome(anyString(), anyString());
    }

    @Test
    void isSuppressMessageId_multipleIdsOneRule_eachFires() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRule("unknown_cs_generic", true,
                        List.of("Terminology_TX_System_Unknown", "Coding_has_no_system__cannot_validate"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertTrue(advisor.isSuppressMessageId("Patient.gender", "Terminology_TX_System_Unknown"));
        assertTrue(advisor.isSuppressMessageId("Patient.gender", "Coding_has_no_system__cannot_validate"));
        verify(metrics, times(2))
                .incrementRuleOutcome(eq("unknown_cs_generic"), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
    }

    @Test
    void isSuppressMessageId_messageIdClaimedByTwoRules_firstClaimerWins() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRule("rule_a", true, List.of("Shared_Message_Id")),
                suppressRule("rule_b", true, List.of("Shared_Message_Id"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertTrue(advisor.isSuppressMessageId("Patient.gender", "Shared_Message_Id"));
        // First rule iterated by the repository wins the counter credit; the outcome (suppress)
        // is identical either way, only attribution differs.
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("rule_a"), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
        verify(metrics, never())
                .incrementRuleOutcome(eq("rule_b"), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
    }

    @Test
    void isSuppressMessageId_acceptableFalseRule_isDemoted() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRule("blocking", false, List.of("Critical_Error_Id"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        // The rule must NOT load — silently suppressing a blocking message would mask failures.
        assertTrue(advisor.getLoadedSuppressMap().isEmpty());
        assertFalse(advisor.isSuppressMessageId("Patient.gender", "Critical_Error_Id"));
    }

    @Test
    void isSuppressMessageId_nullAndBlankIdsAreDropped() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRule("mixed", true,
                        java.util.Arrays.asList("Valid_Id", null, "  ", "Other_Valid_Id"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        // Only the two non-blank IDs should be loaded.
        assertEquals(2, advisor.getLoadedSuppressMap().size());
        assertTrue(advisor.isSuppressMessageId("Patient.gender", "Valid_Id"));
        assertTrue(advisor.isSuppressMessageId("Patient.gender", "Other_Valid_Id"));
    }

    // --- suppressPathPatterns path-narrowing ---------------------------------------------------

    private static Category suppressRuleWithPath(String id, List<String> messageIds, List<String> pathPatterns) {
        Category c = suppressRule(id, true, messageIds);
        c.setSuppressPathPatterns(pathPatterns);
        return c;
    }

    @Test
    void suppressPathPatterns_emptyOrNull_firesOnAnyPath() {
        // Regression: this is the Phase 4 default that unknown_code_system relies on.
        Category nullPaths = suppressRule("unscoped_paths", true, List.of("Some_Message"));
        nullPaths.setSuppressPathPatterns(null);
        Category emptyPaths = suppressRuleWithPath("empty_paths", List.of("Other_Message"), List.of());

        when(categoryRepository.findAll()).thenReturn(List.of(nullPaths, emptyPaths));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        // Both null and empty pathPatterns should behave identically — "fire on any path."
        assertTrue(advisor.isSuppressMessageId("Patient.gender", "Some_Message"));
        assertTrue(advisor.isSuppressMessageId("Encounter.subject", "Some_Message"));
        assertTrue(advisor.isSuppressMessageId("Patient.gender", "Other_Message"));
    }

    @Test
    void suppressPathPatterns_matchingPath_fires() {
        // Path pattern shape mirrors what the medicationrequest_requester_does_not_have_a_proper_reference
        // rule's matcher uses against HAPI's actual expression text (Bundle.entry[N].resource.ofType(...)
        // .where(id='...').requester). The pattern needs to bridge the .ofType(MedicationRequest) ...
        // .requester structure since Pattern.find() looks for the verbatim substring.
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRuleWithPath("med_request_requester",
                        List.of("Reference_REF_NoDisplay"),
                        List.of("ofType\\(MedicationRequest\\).*\\.requester"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertTrue(advisor.isSuppressMessageId(
                "Bundle.entry[3].resource.ofType(MedicationRequest).where(id='X').requester",
                "Reference_REF_NoDisplay"));
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("med_request_requester"), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
    }

    @Test
    void suppressPathPatterns_nonMatchingPath_doesNotFire() {
        // The canonical over-suppress concern: a path-narrowed SUPPRESS rule must not silence
        // the same message ID on unrelated paths.
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRuleWithPath("med_request_requester",
                        List.of("Reference_REF_NoDisplay"),
                        List.of("ofType\\(MedicationRequest\\).*\\.requester"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertFalse(advisor.isSuppressMessageId(
                "Bundle.entry[3].resource.ofType(Encounter).subject",
                "Reference_REF_NoDisplay"));
        verify(metrics, never())
                .incrementRuleOutcome(anyString(), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
    }

    @Test
    void suppressPathPatterns_nullPath_withPatternList_doesNotFire() {
        // HAPI generally passes non-null paths, but defensive: if a path is null, a path-narrowed
        // rule should not fire (we can't verify the narrowing). Only unscoped rules fire on null.
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRuleWithPath("narrow", List.of("Some_Message"),
                        List.of("Patient\\.identifier"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertFalse(advisor.isSuppressMessageId(null, "Some_Message"));
    }

    @Test
    void suppressPathPatterns_multiplePatternsOR() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRuleWithPath("multi",
                        List.of("Some_Message"),
                        List.of("Patient\\.identifier", "Encounter\\.identifier"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        // Either pattern matching is enough.
        assertTrue(advisor.isSuppressMessageId("Patient.identifier[0].system", "Some_Message"));
        assertTrue(advisor.isSuppressMessageId("Encounter.identifier[0].system", "Some_Message"));
        assertFalse(advisor.isSuppressMessageId("Observation.identifier[0].system", "Some_Message"));
    }

    @Test
    void suppressPathPatterns_invalidRegexPartial_otherPatternsStillLoad() {
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRuleWithPath("mixed_paths",
                        List.of("Some_Message"),
                        List.of("[unterminated", "Patient\\.identifier"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        // The valid pattern still loads; the invalid one is logged and dropped.
        assertTrue(advisor.isSuppressMessageId("Patient.identifier[0].system", "Some_Message"));
        assertFalse(advisor.isSuppressMessageId("Encounter.subject", "Some_Message"));
    }

    @Test
    void suppressPathPatterns_allInvalidRegex_demotesNeverFires() {
        // Critical: if every path pattern fails to compile, falling back to "fire on any path"
        // would widen scope to global suppression — exactly the over-suppress the author was
        // trying to avoid. The advisor logs and the rule never matches.
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRuleWithPath("all_bad_paths",
                        List.of("Some_Message"),
                        List.of("[unterminated", "(also-bad"))));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        // Rule is still registered (counters and audit trail), but it never fires.
        assertEquals(List.of("all_bad_paths"),
                advisor.getLoadedSuppressMap().get("Some_Message"));
        assertFalse(advisor.isSuppressMessageId("Patient.identifier[0].system", "Some_Message"));
        assertFalse(advisor.isSuppressMessageId("Encounter.subject", "Some_Message"));
        verify(metrics, never())
                .incrementRuleOutcome(anyString(), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
    }

    @Test
    void suppressPathPatterns_narrowAndBroadRulesShareMessageId_eachWinsOnOwnPath() {
        // Two rules registering the same message ID. The first (narrow, path-scoped) catches
        // its specific path; the second (broad, unscoped) catches everything else. Both can
        // coexist for the same messageId.
        when(categoryRepository.findAll()).thenReturn(List.of(
                suppressRuleWithPath("narrow",
                        List.of("Shared_Message"),
                        List.of("ofType\\(MedicationRequest\\).*\\.requester")),
                suppressRule("broad", true, List.of("Shared_Message"))));  // null pathPatterns
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        // Both rules registered under the shared message ID.
        assertEquals(List.of("narrow", "broad"),
                advisor.getLoadedSuppressMap().get("Shared_Message"));

        // On the narrow rule's path: it wins (iteration order = narrow first).
        advisor.isSuppressMessageId("Bundle.entry[3].resource.ofType(MedicationRequest).where(id='X').requester",
                "Shared_Message");
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("narrow"), eq(ValidationMetrics.OUTCOME_SUPPRESSED));

        // On a different path: narrow misses, broad fires.
        advisor.isSuppressMessageId("Encounter.subject", "Shared_Message");
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("broad"), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
    }

    @Test
    void isSuppressMessageId_ruleWithBothScopeAndSuppressMessageIds_loadsBothHooks() {
        // The unknown_code_system migration shape: scope.codeSystems handles the URL-specific
        // matcher branches via policyForCodedContent; suppressMessageIds handles the generic-
        // shape branches via isSuppressMessageId. Same rule, both hooks loaded.
        Category mixed = skipRule("unknown_code_system", true,
                List.of("https?://open\\.epic\\.com/.*"));
        mixed.setSuppressMessageIds(List.of("Terminology_TX_System_Unknown"));
        when(categoryRepository.findAll()).thenReturn(List.of(mixed));
        CategoryBackedPolicyAdvisor advisor = new CategoryBackedPolicyAdvisor(categoryRepository, metrics);

        assertEquals(List.of("unknown_code_system"), advisor.getLoadedSkipRuleIds());
        assertEquals(1, advisor.getLoadedSuppressMap().size());

        // SKIP hook fires on the URL system
        advisor.policyForCodedContent(
                null, null, "Patient.code.coding[0].system", null, null, null, null, null,
                List.of("https://open.epic.com/FHIR/X"));
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("unknown_code_system"), eq(ValidationMetrics.OUTCOME_SKIPPED));

        // SUPPRESS hook fires on the message ID
        assertTrue(advisor.isSuppressMessageId("Patient.gender", "Terminology_TX_System_Unknown"));
        verify(metrics, times(1))
                .incrementRuleOutcome(eq("unknown_code_system"), eq(ValidationMetrics.OUTCOME_SUPPRESSED));
    }
}
