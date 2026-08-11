package com.lantanagroup.link.validation.services.categoryoverride;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.ValidationPolicyConfig;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategorySeverity;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.CategoryMatchStrategy;
import com.lantanagroup.link.validation.enums.CategoryOverrideScope;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.CategorizationService;
import com.lantanagroup.link.validation.services.execution.EvaluatedFinding;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.Mockito.doAnswer;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.verifyNoInteractions;

@ExtendWith(MockitoExtension.class)
class CategoryOverrideEngineTest {

    @Mock private CategorizationService categorizationService;

    private final ValidationPolicyConfig config = new ValidationPolicyConfig();

    private CategoryOverrideEngine engine;

    @BeforeEach
    void setUp() {
        engine = new CategoryOverrideEngine(
                categorizationService, new CategorySequenceProvider(new ObjectMapper()), config);
    }

    private static Category category(String id, CategorySeverity severity, boolean acceptable) {
        Category category = new Category();
        category.setId(id);
        category.setTitle(id);
        category.setSeverity(severity);
        category.setAcceptable(acceptable);
        category.setGuidance("guidance");
        return category;
    }

    private static RawFinding finding(String checkLocalId, Severity severity) {
        return RawFinding.builder()
                .checkLocalId(checkLocalId)
                .dimension(PiqiDimension.CONFORMANCE)
                .severity(severity)
                .code("fhir-conformance")
                .message("some validator message")
                .build();
    }

    /** Every Result handed to categorization comes back carrying these categories. */
    private void categorizeWith(Category... categories) {
        doAnswer(invocation -> {
            List<Result> results = invocation.getArgument(0);
            results.forEach(r -> r.setCategories(List.of(categories)));
            return null;
        }).when(categorizationService).categorize(anyList());
    }

    private void enable(CategoryMatchStrategy strategy) {
        config.getCategoryOverride().setEnabled(true);
        config.getCategoryOverride().setMatchStrategy(strategy);
    }

    // ------------------------------------------------------------------
    // Feature flag
    // ------------------------------------------------------------------

    @Test
    void disabledLeavesEveryFindingUntouchedAndNeverCategorizes() {
        List<EvaluatedFinding> evaluated = engine.apply(
                List.of(finding("c1", Severity.ERROR), finding("c2", Severity.WARNING)),
                Map.of("c1", CheckType.FHIR_CONFORMANCE, "c2", CheckType.TERMINOLOGY));

        assertEquals(2, evaluated.size());
        assertEquals(Severity.ERROR, evaluated.get(0).effectiveSeverity());
        assertNull(evaluated.get(0).acceptable(), "acceptable must stay unknown when disabled");
        assertTrue(evaluated.get(0).categoryIds().isEmpty());
        // Also a performance guarantee: the disabled path must not read the category table at all.
        verifyNoInteractions(categorizationService);
    }

    @Test
    void enabledWithNoMatchesBehavesExactlyLikeDisabled() {
        enable(CategoryMatchStrategy.WORST_OF);
        categorizeWith();

        List<EvaluatedFinding> evaluated = engine.apply(
                List.of(finding("c1", Severity.ERROR)), Map.of("c1", CheckType.FHIR_CONFORMANCE));

        assertEquals(Severity.ERROR, evaluated.get(0).effectiveSeverity());
        assertNull(evaluated.get(0).acceptable());
    }

    @Test
    void noFindingsProducesNoDecisions() {
        assertTrue(engine.apply(List.of(), Map.of()).isEmpty());
    }

    // ------------------------------------------------------------------
    // WORST_OF
    // ------------------------------------------------------------------

    @Test
    void aSingleCategoryAppliesItsSeverityAndAcceptability() {
        enable(CategoryMatchStrategy.WORST_OF);
        categorizeWith(category("cat-a", CategorySeverity.WARNING, true));

        EvaluatedFinding evaluated = engine.apply(
                List.of(finding("c1", Severity.ERROR)), Map.of("c1", CheckType.FHIR_CONFORMANCE)).get(0);

        assertEquals(Severity.ERROR, evaluated.originalSeverity());
        assertEquals(Severity.WARNING, evaluated.effectiveSeverity());
        assertEquals(Boolean.TRUE, evaluated.acceptable());
        assertEquals(List.of("cat-a"), evaluated.categoryIds());
        assertEquals("cat-a", evaluated.governingCategoryId());
        assertTrue(evaluated.severityWasOverridden());
    }

    @Test
    void unacceptableBeatsAcceptableInEitherDeclarationOrder() {
        enable(CategoryMatchStrategy.WORST_OF);

        categorizeWith(category("a-yes", CategorySeverity.WARNING, true),
                category("b-no", CategorySeverity.WARNING, false));
        assertEquals(Boolean.FALSE, applyOne().acceptable());

        categorizeWith(category("a-no", CategorySeverity.WARNING, false),
                category("b-yes", CategorySeverity.WARNING, true));
        assertEquals(Boolean.FALSE, applyOne().acceptable());
    }

    @Test
    void theHighestSeverityWins() {
        enable(CategoryMatchStrategy.WORST_OF);
        categorizeWith(category("a", CategorySeverity.INFORMATION, true),
                category("b", CategorySeverity.ERROR, true),
                category("c", CategorySeverity.WARNING, true));

        assertEquals(Severity.ERROR, applyOne().effectiveSeverity());
    }

    /**
     * Severity and acceptable are combined independently, so the outcome can be a pair no single
     * category declares — ERROR from one, unacceptable from the other.
     */
    @Test
    void severityAndAcceptabilityAreCombinedIndependently() {
        enable(CategoryMatchStrategy.WORST_OF);
        categorizeWith(category("a-loud-but-fine", CategorySeverity.ERROR, true),
                category("b-quiet-but-fatal", CategorySeverity.WARNING, false));

        EvaluatedFinding evaluated = applyOne();

        assertEquals(Severity.ERROR, evaluated.effectiveSeverity());
        assertEquals(Boolean.FALSE, evaluated.acceptable());
        assertEquals("b-quiet-but-fatal", evaluated.governingCategoryId(), "unacceptable governs");
    }

    /**
     * Neither category is in categories.json here, so both are unsequenced and the documented final
     * tie-break — category id — decides. Ordering stays total either way.
     */
    @Test
    void tiesFallBackToCategoryIdOrder() {
        enable(CategoryMatchStrategy.WORST_OF);
        categorizeWith(category("zulu", CategorySeverity.WARNING, true),
                category("alpha", CategorySeverity.WARNING, true));

        assertEquals("alpha", applyOne().governingCategoryId());
    }

    // ------------------------------------------------------------------
    // FIRST_MATCH
    // ------------------------------------------------------------------

    @Test
    void firstMatchUsesOneCategoryOnly() {
        enable(CategoryMatchStrategy.FIRST_MATCH);
        categorizeWith(category("zulu", CategorySeverity.ERROR, false),
                category("alpha", CategorySeverity.INFORMATION, true));

        EvaluatedFinding evaluated = applyOne();

        assertEquals(List.of("alpha"), evaluated.categoryIds(), "lowest sequence, then id");
        assertEquals(Severity.INFORMATION, evaluated.effectiveSeverity());
        assertEquals(Boolean.TRUE, evaluated.acceptable());
    }

    // ------------------------------------------------------------------
    // Scope
    // ------------------------------------------------------------------

    @Test
    void fhirOnlyScopeSkipsFindingsFromOtherCheckTypes() {
        enable(CategoryMatchStrategy.WORST_OF);
        config.getCategoryOverride().setScope(CategoryOverrideScope.FHIR_ONLY);
        categorizeWith(category("cat-a", CategorySeverity.INFORMATION, true));

        List<EvaluatedFinding> evaluated = engine.apply(
                List.of(finding("c-conformance", Severity.ERROR), finding("c-terminology", Severity.ERROR)),
                Map.of("c-conformance", CheckType.FHIR_CONFORMANCE, "c-terminology", CheckType.TERMINOLOGY));

        assertEquals(Severity.INFORMATION, evaluated.get(0).effectiveSeverity());
        assertEquals(Severity.ERROR, evaluated.get(1).effectiveSeverity(), "terminology finding untouched");
        assertNull(evaluated.get(1).acceptable());
    }

    @Test
    void fhirOnlyScopeWithNothingInScopeSkipsCategorizationEntirely() {
        enable(CategoryMatchStrategy.WORST_OF);
        config.getCategoryOverride().setScope(CategoryOverrideScope.FHIR_ONLY);

        List<EvaluatedFinding> evaluated = engine.apply(
                List.of(finding("c-terminology", Severity.ERROR)), Map.of("c-terminology", CheckType.TERMINOLOGY));

        assertNull(evaluated.get(0).acceptable());
        verifyNoInteractions(categorizationService);
    }

    // ------------------------------------------------------------------
    // Resilience
    // ------------------------------------------------------------------

    /** One unusable rule in the category table must not fail the whole evaluation. */
    @Test
    void aCategorizationFailureFallsBackToUntouchedFindings() {
        enable(CategoryMatchStrategy.WORST_OF);
        doThrow(new IllegalStateException("No pattern specified"))
                .when(categorizationService).categorize(anyList());

        List<EvaluatedFinding> evaluated = engine.apply(
                List.of(finding("c1", Severity.ERROR)), Map.of("c1", CheckType.FHIR_CONFORMANCE));

        assertEquals(Severity.ERROR, evaluated.get(0).effectiveSeverity());
        assertNull(evaluated.get(0).acceptable());
    }

    @Test
    void aNullCheckTypeMapIsToleratedUnderAllChecks() {
        enable(CategoryMatchStrategy.WORST_OF);
        categorizeWith(category("cat-a", CategorySeverity.INFORMATION, true));

        List<EvaluatedFinding> evaluated = engine.apply(List.of(finding("c1", Severity.ERROR)), null);

        assertEquals(Severity.INFORMATION, evaluated.get(0).effectiveSeverity());
    }

    private EvaluatedFinding applyOne() {
        return engine.apply(List.of(finding("c1", Severity.ERROR)), Map.of("c1", CheckType.FHIR_CONFORMANCE)).get(0);
    }
}
