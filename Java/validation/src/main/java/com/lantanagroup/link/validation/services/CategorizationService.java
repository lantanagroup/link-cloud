package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.Timer;
import com.lantanagroup.link.shared.utils.DiagnosticNames;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategoryRule;
import com.lantanagroup.link.validation.entities.CategorySnapshot;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import com.lantanagroup.link.validation.repositories.CategoryRuleRepository;
import com.lantanagroup.link.validation.repositories.ResultRepository;
import io.opentelemetry.api.common.Attributes;
import jakarta.transaction.Transactional;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.context.annotation.Scope;
import org.springframework.context.annotation.ScopedProxyMode;
import org.springframework.core.io.Resource;
import org.springframework.core.io.support.PathMatchingResourcePatternResolver;
import org.springframework.stereotype.Service;

import java.io.IOException;
import java.io.InputStream;
import java.util.Arrays;
import java.util.Collection;
import java.util.HashSet;
import java.util.List;
import java.util.Objects;
import java.util.Set;
import java.util.stream.Collectors;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class CategorizationService {
    private static final Logger logger = LoggerFactory.getLogger(CategorizationService.class);

    private final ObjectMapper objectMapper;
    private final CategoryRepository categoryRepository;
    private final CategoryRuleRepository categoryRuleRepository;
    private final ResultRepository resultRepository;
    private final ValidationMetrics metrics;

    public CategorizationService(
            ObjectMapper objectMapper,
            CategoryRepository categoryRepository,
            CategoryRuleRepository categoryRuleRepository,
            ResultRepository resultRepository,
            ValidationMetrics metrics) {
        this.objectMapper = objectMapper;
        this.categoryRepository = categoryRepository;
        this.categoryRuleRepository = categoryRuleRepository;
        this.resultRepository = resultRepository;
        this.metrics = metrics;
    }

    /**
     * Removes any persisted categories (and their associated category_rule and result_category rows)
     * whose IDs are not present in {@code keepIds}. Intended to be called before saving an authoritative
     * set of categories (e.g. on $initialize or $bulk-import) so that stale categories do not linger.
     */
    @Transactional
    public void removeObsoleteCategories(Collection<String> keepIds) {
        Set<String> keepSet = new HashSet<>(keepIds);
        List<String> obsoleteIds = categoryRepository.findAll().stream()
                .map(Category::getId)
                .filter(id -> !keepSet.contains(id))
                .collect(Collectors.toList());
        if (obsoleteIds.isEmpty()) {
            return;
        }
        logger.info("Removing {} obsolete categories: {}", obsoleteIds.size(), obsoleteIds);
        resultRepository.deleteByCategoryIds(obsoleteIds);
        categoryRuleRepository.deleteByCategoryIdIn(obsoleteIds);
        categoryRepository.deleteByIdIn(obsoleteIds);
        categoryRepository.flush();
    }

    @Transactional
    public void saveCategorySnapshot(CategorySnapshot categorySnapshot) {
        Category category = categoryRepository.findById(categorySnapshot.getId())
                .orElseGet(categorySnapshot::toCategory);
        categoryRepository.save(category);
        CategoryRule categoryRule = categorySnapshot.toCategoryRule(category);
        categoryRuleRepository.save(categoryRule);
    }

    @Transactional
    public void initializeCategories() throws IOException {
        logger.info("Initializing categories");
        PathMatchingResourcePatternResolver resolver = new PathMatchingResourcePatternResolver();
        Resource resource = resolver.getResource("classpath:categories.json");
        try (InputStream stream = resource.getInputStream()) {
            CategorySnapshot[] categorySnapshots = objectMapper.readValue(stream, CategorySnapshot[].class);
            Set<String> keepIds = Arrays.stream(categorySnapshots)
                    .map(CategorySnapshot::getId)
                    .collect(Collectors.toSet());
            removeObsoleteCategories(keepIds);
            for (CategorySnapshot categorySnapshot : categorySnapshots) {
                logger.debug("Initializing category: {}", categorySnapshot.getId());
                saveCategorySnapshot(categorySnapshot);
            }
        }
    }

    private void doCategorize(List<Result> results, List<CategoryRule> categoryRules) {
        results.parallelStream().forEach(result -> {
            List<Category> categories = categoryRules.stream()
                    .filter(Objects::nonNull)
                    .filter(categoryRule -> categoryRule.getMatcher().isMatch(result))
                    .map(CategoryRule::getCategory)
                    .toList();
            result.setCategories(categories);
            // Counter wiring for Phase 1 observability. Every LABEL match is recorded so we can
            // see, per rule, how often it actually fires. Pairs with the SKIP/SUPPRESS counters in
            // CategoryBackedPolicyAdvisor.
            if (metrics != null) {
                for (Category c : categories) {
                    metrics.incrementRuleOutcome(c.getId(), ValidationMetrics.OUTCOME_LABELED);
                }
            }
        });
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
