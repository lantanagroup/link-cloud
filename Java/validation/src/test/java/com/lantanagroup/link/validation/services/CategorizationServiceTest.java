package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategoryRule;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ResultField;
import com.lantanagroup.link.validation.matchers.RegexMatcher;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import com.lantanagroup.link.validation.repositories.CategoryRuleRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;

import java.util.ArrayList;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.when;

public class CategorizationServiceTest {
    @Mock
    private CategoryRepository categoryRepository;

    @Mock
    private CategoryRuleRepository categoryRuleRepository;

    @InjectMocks
    private CategorizationService categorizationService;

    @BeforeEach
    void setUp() {
        MockitoAnnotations.openMocks(this);
    }

    @Test
    void categorizeWithMissingRuleDoesNotThrow() {
        Category withRule = new Category();
        withRule.setId("with");
        CategoryRule rule = new CategoryRule();
        rule.setCategory(withRule);
        rule.setMatcher(result -> true);
        withRule.setRules(List.of(rule));

        Category withoutRule = new Category();
        withoutRule.setId("without");
        withoutRule.setRules(null);

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
}

