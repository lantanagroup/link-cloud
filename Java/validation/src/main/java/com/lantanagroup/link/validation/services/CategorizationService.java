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
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.TreeMap;
import java.util.stream.Collectors;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class CategorizationService {
    private static final Logger logger = LoggerFactory.getLogger(CategorizationService.class);

    private final ObjectMapper objectMapper;
    private final CategoryRepository categoryRepository;
    private final CategoryRuleRepository categoryRuleRepository;
    private final ResultRepository resultRepository;

    public CategorizationService(
            ObjectMapper objectMapper,
            CategoryRepository categoryRepository,
            CategoryRuleRepository categoryRuleRepository,
            ResultRepository resultRepository) {
        this.objectMapper = objectMapper;
        this.categoryRepository = categoryRepository;
        this.categoryRuleRepository = categoryRuleRepository;
        this.resultRepository = resultRepository;
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
        // Category has an assigned (non-generated) ID, so save() always routes through merge(). For an
        // ID that isn't in the database yet, merge() returns a *different* managed instance than the one
        // passed in — the rule has to be attached to that instance, or the persistence context ends up
        // holding two representations of the same category.
        Category category = categoryRepository.findById(categorySnapshot.getId())
                .map(categorySnapshot::applyTo)
                .orElseGet(categorySnapshot::toCategory);
        category = categoryRepository.save(category);
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
        });

        // Shared by the legacy $categorize flow and the rubric engine's category override, so both
        // paths log how many results a category claimed (and which categories did the claiming).
        long matched = results.stream()
                .filter(r -> r.getCategories() != null && !r.getCategories().isEmpty())
                .count();
        Map<String, Long> byCategory = results.stream()
                .filter(r -> r.getCategories() != null)
                .flatMap(r -> r.getCategories().stream())
                .collect(Collectors.groupingBy(Category::getId, TreeMap::new, Collectors.counting()));

        logger.info("Categorization matched {} of {} result(s) against {} category rule(s){}",
                matched, results.size(), categoryRules.size(),
                byCategory.isEmpty() ? "" : "; matches by category: " + byCategory);
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
