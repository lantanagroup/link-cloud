package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.Timer;
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
import org.springframework.core.io.Resource;
import org.springframework.core.io.support.PathMatchingResourcePatternResolver;
import org.springframework.stereotype.Service;

import java.io.IOException;
import java.io.InputStream;
import java.util.List;
import java.util.Objects;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class CategorizationService {
    private static final Logger logger = LoggerFactory.getLogger(CategorizationService.class);

    private final ObjectMapper objectMapper;
    private final CategoryRepository categoryRepository;
    private final CategoryRuleRepository categoryRuleRepository;
    private final MetricService metricService;

    public CategorizationService(
            ObjectMapper objectMapper,
            CategoryRepository categoryRepository,
            CategoryRuleRepository categoryRuleRepository,
            MetricService metricService) {
        this.objectMapper = objectMapper;
        this.categoryRepository = categoryRepository;
        this.categoryRuleRepository = categoryRuleRepository;
        this.metricService = metricService;
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
        PathMatchingResourcePatternResolver resolver = new PathMatchingResourcePatternResolver();
        Resource resource = resolver.getResource("classpath:categories.json");
        try (InputStream stream = resource.getInputStream()) {
            CategorySnapshot[] categorySnapshots = objectMapper.readValue(stream, CategorySnapshot[].class);
            for (CategorySnapshot categorySnapshot : categorySnapshots) {
                logger.debug("Initializing category: {}", categorySnapshot.getId());
                saveCategorySnapshot(categorySnapshot);
            }
        }
    }

    private void doCategorize(List<Result> results, List<CategoryRule> categoryRules) {
        try (Timer timer = Timer.start()) {
            results.parallelStream().forEach(result -> {
                List<Category> categories = categoryRules.stream()
                        .filter(Objects::nonNull)
                        .filter(categoryRule -> categoryRule.getMatcher().isMatch(result))
                        .map(CategoryRule::getCategory)
                        .toList();
                result.setCategories(categories);
            });

            this.metricService.getCategorizationDurationUpDown().add((long) timer.getSeconds());
            logger.debug("Categorization completed in {} seconds", String.format("%.2f", timer.getSeconds()));
        }
    }

    public void categorize(List<Result> results) {
        doCategorize(results, categoryRepository.findAll().stream()
                .map(Category::getLatestRule)
                .filter(Objects::nonNull)
                .toList());
    }

    public void categorize(List<Result> results, List<CategorySnapshot> categorySnapshots) {
        doCategorize(results, categorySnapshots.stream()
                .map(CategorySnapshot::toCategoryRule)
                .toList());
    }
}
