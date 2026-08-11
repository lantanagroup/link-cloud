package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategoryRule;
import com.lantanagroup.link.validation.entities.CategorySeverity;
import com.lantanagroup.link.validation.entities.CategorySnapshot;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ResultField;
import com.lantanagroup.link.validation.matchers.RegexMatcher;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import com.lantanagroup.link.validation.repositories.CategoryRuleRepository;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.boot.test.autoconfigure.orm.jpa.TestEntityManager;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;

import java.io.IOException;
import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * JPA-slice coverage for the $initialize / $bulk-import category replacement flow.
 *
 * <p>The other tests for {@link CategorizationService} mock {@link CategoryRepository}, which erases the
 * persistence-context semantics this flow depends on — most importantly that {@code save()} routes
 * through {@code merge()} for assigned IDs and therefore returns a different instance than it was given.
 * These tests run against a real Hibernate session so that behaviour is exercised.
 *
 * <p>The seeded state models an environment upgrading over pre-existing data: category IDs that predate
 * the R2 rename, accumulated rule history, and result_category rows from previous reports. None of that
 * occurs against a freshly-created local database, which is why this flow was never exercised locally.
 *
 * <p>NOTE: these tests do <em>not</em> cover the separate "Found shared references to a collection:
 * Category.rules" failure. That one needs a case-insensitive collation, which H2 cannot emulate: it
 * occurs when category.id and category_rule.category_id hold the same ID in different casing, so the
 * eager Category -> rules -> category fetch resolves one row into two entity keys and builds two
 * Category instances for it. Reproducing it requires a real SQL Server. The underlying cause — IDs
 * compared case-sensitively in Java but case-insensitively by the database — is still open.
 */
// MSSQLServer compatibility mode so the SQL-Server-specific "varchar(max)" column definitions on
// CategoryRule.matcher and Result.message are accepted by the generated DDL.
@DataJpaTest(properties = {
        "spring.datasource.url=jdbc:h2:mem:categoryinit;MODE=MSSQLServer;DB_CLOSE_DELAY=-1",
        "spring.datasource.driver-class-name=org.h2.Driver",
        "spring.jpa.hibernate.ddl-auto=create-drop"
})
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import(CategoryInitializationJpaTest.Config.class)
class CategoryInitializationJpaTest {

    /**
     * Category IDs in the style used before the R2 rename (LNK-4873). None of these appear in the
     * shipped categories.json, so every incoming snapshot takes the "not currently persisted" path.
     */
    private static final List<String> LEGACY_IDS = List.of(
            "Unknown_Code_System",
            "Unknown_Extension",
            "Unable_to_match_profile",
            "Invalid_code");

    @TestConfiguration
    static class Config {
        /** Also required by MatcherConverter, which Hibernate resolves from the application context. */
        @Bean
        ObjectMapper objectMapper() {
            return new ObjectMapper();
        }

        @Bean
        CategorizationService categorizationService(
                ObjectMapper objectMapper,
                CategoryRepository categoryRepository,
                CategoryRuleRepository categoryRuleRepository,
                ResultRepository resultRepository) {
            return new CategorizationService(
                    objectMapper,
                    categoryRepository,
                    categoryRuleRepository,
                    resultRepository);
        }
    }

    /** Matcher.class uses DEDUCTION type resolution, so the properties have to be set to round-trip. */
    private static RegexMatcher matcher() {
        RegexMatcher matcher = new RegexMatcher();
        matcher.setField(ResultField.MESSAGE);
        matcher.setRegex("anything");
        return matcher;
    }

    @Autowired
    private CategorizationService categorizationService;

    @Autowired
    private CategoryRepository categoryRepository;

    @Autowired
    private TestEntityManager testEntityManager;

    /**
     * Persists a category and one rule for it, mirroring what a previous $initialize would have left
     * behind. Rules matter: they are what makes Category.rules a non-empty eagerly-loaded collection.
     */
    private void seedCategory(String id, int ruleCount) {
        Category category = new Category();
        category.setId(id);
        category.setTitle(id);
        category.setSeverity(CategorySeverity.WARNING);
        category.setAcceptable(false);
        category.setGuidance("seeded");
        testEntityManager.persist(category);

        // Repeated $initialize / $bulk-import calls append rules rather than replacing them, so a
        // long-lived environment accumulates rule history per category.
        for (int i = 0; i < ruleCount; i++) {
            CategoryRule rule = new CategoryRule();
            rule.setCategory(category);
            rule.setMatcher(matcher());
            testEntityManager.persist(rule);
        }

        // A validated report leaves result_category rows pointing at the category.
        Result result = new Result();
        result.setFacilityId("facility");
        result.setReportId("report");
        result.setPatientId("patient-" + id);
        result.setSeverity(OperationOutcome.IssueSeverity.WARNING);
        result.setCode(OperationOutcome.IssueType.INVALID);
        result.setMessage("seeded result for " + id);
        result.setCategories(List.of(category));
        testEntityManager.persist(result);
    }

    /**
     * Detaches everything so the service starts from the same state a fresh HTTP request would: rows in
     * the database, nothing yet in the persistence context.
     */
    private void seedLegacyCategories() {
        LEGACY_IDS.forEach(id -> seedCategory(id, 3));
        testEntityManager.flush();
        testEntityManager.clear();
    }

    @Test
    void initializeReplacesLegacyCategoriesWithoutSharedCollectionFailure() throws IOException {
        seedLegacyCategories();

        assertDoesNotThrow(() -> categorizationService.initializeCategories());
        // The failure this guards against surfaces at flush, not at call time.
        assertDoesNotThrow(() -> testEntityManager.flush());
        testEntityManager.clear();

        Set<String> persistedIds = categoryRepository.findAll().stream()
                .map(Category::getId)
                .collect(Collectors.toSet());

        LEGACY_IDS.forEach(id -> assertFalse(persistedIds.contains(id), "legacy category not removed: " + id));
        assertEquals(shippedIds(), persistedIds, "persisted categories should match categories.json");
    }

    @Test
    void initializeGivesEveryShippedCategoryARule() throws IOException {
        seedLegacyCategories();

        categorizationService.initializeCategories();
        testEntityManager.flush();
        testEntityManager.clear();

        for (Category category : categoryRepository.findAll()) {
            assertNotNull(category.getLatestRule(), "no rule persisted for category: " + category.getId());
        }
    }

    @Test
    void reimportUpdatesFieldsOnAnAlreadyPersistedCategory() {
        String id = "unknown_code_system";
        CategorySnapshot original = snapshot(id, "Original title", "Original guidance", CategorySeverity.WARNING);
        categorizationService.saveCategorySnapshot(original);
        testEntityManager.flush();
        testEntityManager.clear();

        CategorySnapshot revised = snapshot(id, "Revised title", "Revised guidance", CategorySeverity.ERROR);
        categorizationService.saveCategorySnapshot(revised);
        testEntityManager.flush();
        testEntityManager.clear();

        Category persisted = categoryRepository.findById(id).orElseThrow();
        assertEquals("Revised title", persisted.getTitle());
        assertEquals("Revised guidance", persisted.getGuidance());
        assertEquals(CategorySeverity.ERROR, persisted.getSeverity());
        assertTrue(persisted.isAcceptable());
        assertEquals(2, persisted.getRules().size(), "each import should append a rule");
    }

    private CategorySnapshot snapshot(String id, String title, String guidance, CategorySeverity severity) {
        CategorySnapshot snapshot = new CategorySnapshot();
        snapshot.setId(id);
        snapshot.setTitle(title);
        snapshot.setGuidance(guidance);
        snapshot.setSeverity(severity);
        snapshot.setAcceptable(severity == CategorySeverity.ERROR);
        snapshot.setMatcher(matcher());
        return snapshot;
    }

    private Set<String> shippedIds() throws IOException {
        return CategoryFixtures.loadShippedCategories().stream()
                .map(Category::getId)
                .collect(Collectors.toSet());
    }
}
