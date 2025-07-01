package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategoryRule;
import com.lantanagroup.link.validation.entities.Result;
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
}

