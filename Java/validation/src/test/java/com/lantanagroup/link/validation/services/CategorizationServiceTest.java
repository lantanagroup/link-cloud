package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategoryRule;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ResultField;
import com.lantanagroup.link.validation.matchers.CompositeMatcher;
import com.lantanagroup.link.validation.matchers.RegexMatcher;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import io.opentelemetry.api.metrics.LongUpDownCounter;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;

import java.util.ArrayList;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

public class CategorizationServiceTest {
    @Mock
    private CategoryRepository categoryRepository;

    @Mock
    private MetricService metricService;

    @InjectMocks
    private CategorizationService categorizationService;

    @BeforeEach
    void setUp() {
        MockitoAnnotations.openMocks(this);

        LongUpDownCounter counter = mock(LongUpDownCounter.class);
        when(metricService.getCategorizationDurationUpDown()).thenReturn(counter);
    }

    @Test
    void categorizeWithMissingRuleDoesNotThrow() {
        Category withRule = new Category();
        withRule.setId("with");
        CategoryRule rule = new CategoryRule();
        rule.setCategory(withRule);
        rule.setMatcher(result -> true);
        withRule.setRules(List.of(rule));

        Category withoutRule = mock(Category.class);
        when(withoutRule.getLatestRule()).thenReturn(null);
        when(withoutRule.getRules()).thenReturn(null);
        when(withoutRule.getId()).thenReturn("without");

        when(categoryRepository.findAll()).thenReturn(List.of(withRule, withoutRule));

        Result result = new Result();
        List<Result> results = new ArrayList<>();
        results.add(result);

        assertDoesNotThrow(() -> categorizationService.categorize(results));
        assertNotNull(result.getCategories());
        assertTrue(result.getCategories().contains(withRule));
        assertFalse(result.getCategories().contains(withoutRule));
    }

    @Test
    void categorizeAssignsCategoryWhenRegexMatches() {
        Category category = new Category();
        category.setId("Incorrect_display_value_for_code");

        RegexMatcher matcher = new RegexMatcher();
        matcher.setField(ResultField.MESSAGE);
        matcher.setRegex("^Wrong Display Name '.*' for .* should be .*'.*' .*");

        CategoryRule rule = new CategoryRule();
        rule.setCategory(category);
        rule.setMatcher(matcher);
        category.setRules(List.of(rule));

        when(categoryRepository.findAll()).thenReturn(List.of(category));

        Result result = new Result();
        result.setMessage("Wrong Display Name 'foo' for bar should be baz 'qux' 123");

        categorizationService.categorize(List.of(result));

        assertNotNull(result.getCategories());
        assertTrue(result.getCategories().contains(category));
    }

    @Test
    void categorizeDoesNotAssignCategoryWhenRegexDoesNotMatch() {
        Category category = new Category();
        category.setId("Incorrect_display_value_for_code");

        RegexMatcher matcher = new RegexMatcher();
        matcher.setField(ResultField.MESSAGE);
        matcher.setRegex("^Wrong Display Name '.*' for .* should be .*'.*' .*");

        CategoryRule rule = new CategoryRule();
        rule.setCategory(category);
        rule.setMatcher(matcher);
        category.setRules(List.of(rule));

        when(categoryRepository.findAll()).thenReturn(List.of(category));

        Result result = new Result();
        result.setMessage("Some other message");

        categorizationService.categorize(List.of(result));

        assertNotNull(result.getCategories());
        assertFalse(result.getCategories().contains(category));
    }

    @Test
    void categorizeAssignsCategoryForAnyCompositeChildMatch() {
        Category category = new Category();
        category.setId("Unknown_Code_System");

        RegexMatcher m1 = new RegexMatcher();
        m1.setField(ResultField.MESSAGE);
        m1.setRegex("^Unknown Code System '.*'$");

        RegexMatcher m2 = new RegexMatcher();
        m2.setField(ResultField.MESSAGE);
        m2.setRegex("A code with no system .* A system should be provided");

        RegexMatcher m3 = new RegexMatcher();
        m3.setField(ResultField.MESSAGE);
        m3.setRegex("^CodeSystem is unknown and can't be validated");

        CompositeMatcher matcher = new CompositeMatcher();
        matcher.setChildren(List.of(m1, m2, m3));
        matcher.setRequiresAllChildren(false);

        CategoryRule rule = new CategoryRule();
        rule.setCategory(category);
        rule.setMatcher(matcher);
        category.setRules(List.of(rule));

        when(categoryRepository.findAll()).thenReturn(List.of(category));

        Result r1 = new Result();
        r1.setMessage("Unknown Code System 'foo'");

        Result r2 = new Result();
        r2.setMessage("A code with no system bar A system should be provided");

        Result r3 = new Result();
        r3.setMessage("CodeSystem is unknown and can't be validated");

        List<Result> results = List.of(r1, r2, r3);
        categorizationService.categorize(results);

        for (Result r : results) {
            assertTrue(r.getCategories().contains(category));
        }
    }

    @Test
    void categorizeDoesNotAssignCategoryWhenCompositeNoneMatch() {
        Category category = new Category();
        category.setId("Unknown_Code_System");

        RegexMatcher m1 = new RegexMatcher();
        m1.setField(ResultField.MESSAGE);
        m1.setRegex("^Unknown Code System '.*'$");

        RegexMatcher m2 = new RegexMatcher();
        m2.setField(ResultField.MESSAGE);
        m2.setRegex("A code with no system .* A system should be provided");

        RegexMatcher m3 = new RegexMatcher();
        m3.setField(ResultField.MESSAGE);
        m3.setRegex("^CodeSystem is unknown and can't be validated");

        CompositeMatcher matcher = new CompositeMatcher();
        matcher.setChildren(List.of(m1, m2, m3));
        matcher.setRequiresAllChildren(false);

        CategoryRule rule = new CategoryRule();
        rule.setCategory(category);
        rule.setMatcher(matcher);
        category.setRules(List.of(rule));

        when(categoryRepository.findAll()).thenReturn(List.of(category));

        Result result = new Result();
        result.setMessage("Something unrelated");

        categorizationService.categorize(List.of(result));

        assertFalse(result.getCategories().contains(category));
    }

    @Test
    void categorizeAssignsCategoryWhenAllCompositeChildrenMatch() {
        Category category = new Category();
        category.setId("Unresolved_Code_System");

        RegexMatcher m1 = new RegexMatcher();
        m1.setField(ResultField.MESSAGE);
        m1.setRegex("^URL value '.*' does not resolve");

        RegexMatcher m2 = new RegexMatcher();
        m2.setField(ResultField.EXPRESSION);
        m2.setRegex("\\.coding\\[[0-9]+\\]\\.system");

        CompositeMatcher matcher = new CompositeMatcher();
        matcher.setChildren(List.of(m1, m2));
        matcher.setRequiresAllChildren(true);

        CategoryRule rule = new CategoryRule();
        rule.setCategory(category);
        rule.setMatcher(matcher);
        category.setRules(List.of(rule));

        when(categoryRepository.findAll()).thenReturn(List.of(category));

        Result result = new Result();
        result.setMessage("URL value 'http://foo' does not resolve");
        result.setExpression("Patient.code.coding[0].system");

        categorizationService.categorize(List.of(result));

        assertTrue(result.getCategories().contains(category));
    }

    @Test
    void categorizeDoesNotAssignCategoryWhenCompositeMissingChildMatch() {
        Category category = new Category();
        category.setId("Unresolved_Code_System");

        RegexMatcher m1 = new RegexMatcher();
        m1.setField(ResultField.MESSAGE);
        m1.setRegex("^URL value '.*' does not resolve");

        RegexMatcher m2 = new RegexMatcher();
        m2.setField(ResultField.EXPRESSION);
        m2.setRegex("\\.coding\\[[0-9]+\\]\\.system");

        CompositeMatcher matcher = new CompositeMatcher();
        matcher.setChildren(List.of(m1, m2));
        matcher.setRequiresAllChildren(true);

        CategoryRule rule = new CategoryRule();
        rule.setCategory(category);
        rule.setMatcher(matcher);
        category.setRules(List.of(rule));

        when(categoryRepository.findAll()).thenReturn(List.of(category));

        Result result = new Result();
        result.setMessage("URL value 'http://foo' does not resolve");
        result.setExpression("other.field");

        categorizationService.categorize(List.of(result));

        assertFalse(result.getCategories().contains(category));
    }
}

