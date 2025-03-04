package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategoryRule;
import com.lantanagroup.link.validation.entities.CategorySnapshot;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import com.lantanagroup.link.validation.repositories.CategoryRuleRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.context.annotation.Scope;
import org.springframework.context.annotation.ScopedProxyMode;
import org.springframework.stereotype.Service;

import java.io.IOException;
import java.io.InputStream;
import java.util.List;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class CategorizationService {
    private static final Logger logger = LoggerFactory.getLogger(CategorizationService.class);

    private final ObjectMapper objectMapper;
    private final CategoryRepository categoryRepository;
    private final CategoryRuleRepository categoryRuleRepository;

    public CategorizationService(
            ObjectMapper objectMapper,
            CategoryRepository categoryRepository,
            CategoryRuleRepository categoryRuleRepository) {
        this.objectMapper = objectMapper;
        this.categoryRepository = categoryRepository;
        this.categoryRuleRepository = categoryRuleRepository;
    }

    public void saveCategorySnapshot(CategorySnapshot categorySnapshot) {
        Category category = categoryRepository.findById(categorySnapshot.getId())
                .orElseGet(categorySnapshot::toCategory);
        categoryRepository.save(category);
        CategoryRule categoryRule = categorySnapshot.toCategoryRule(category);
        categoryRuleRepository.save(categoryRule);
    }

    public void initializeCategories() throws IOException {
        logger.info("Initializing categories");
        try (InputStream stream = ClassLoader.getSystemResourceAsStream("categories.json")) {
            CategorySnapshot[] categorySnapshots = objectMapper.readValue(stream, CategorySnapshot[].class);
            for (CategorySnapshot categorySnapshot : categorySnapshots) {
                logger.debug("Initializing category: {}", categorySnapshot.getId());
                saveCategorySnapshot(categorySnapshot);
            }
        }
    }

    private void doCategorize(List<Result> results, List<CategoryRule> categoryRules) {
        results.parallelStream().forEach(result -> {
            List<Category> categories = categoryRules.stream()
                    .filter(categoryRule -> categoryRule.getMatcher().isMatch(result))
                    .map(CategoryRule::getCategory)
                    .toList();
            result.setCategories(categories);
        });
    }

    public void categorize(List<Result> results) {
        doCategorize(results, categoryRepository.findAll().stream()
                .map(Category::getLatestRule)
                .toList());
    }

    public void categorize(List<Result> results, List<CategorySnapshot> categorySnapshots) {
        doCategorize(results, categorySnapshots.stream()
                .map(CategorySnapshot::toCategoryRule)
                .toList());
    }
}
