package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.CategoryRule;
import com.lantanagroup.link.validation.entities.CategorySnapshot;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.matchers.Matcher;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import com.lantanagroup.link.validation.repositories.CategoryRuleRepository;
import com.lantanagroup.link.validation.services.CategorizationService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import jakarta.transaction.Transactional;
import org.apache.commons.collections4.CollectionUtils;
import org.apache.commons.lang3.StringUtils;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;
import org.springframework.web.server.ServerErrorException;

import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

@RestController
@RequestMapping("/api/validation/category")
@SecurityRequirement(name = "bearer-key")
public class CategoryController {
    private final CategoryRepository categoryRepository;
    private final CategoryRuleRepository categoryRuleRepository;
    private final CategorizationService categorizationService;

    public CategoryController(
            CategoryRepository categoryRepository,
            CategoryRuleRepository categoryRuleRepository,
            CategorizationService categorizationService) {
        this.categoryRepository = categoryRepository;
        this.categoryRuleRepository = categoryRuleRepository;
        this.categorizationService = categorizationService;
    }

    private String formatReason(String reason, Integer index) {
        return index == null ? reason : String.format("%s at index %d", reason, index);
    }

    private void validateCategory(Category category, Integer index) {
        if (StringUtils.isEmpty(category.getId())) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, formatReason("No ID provided", index));
        }
        if (StringUtils.isEmpty(category.getTitle())) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, formatReason("No title provided", index));
        }
        if (category.getSeverity() == null) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, formatReason("No severity provided", index));
        }
        if (StringUtils.isEmpty(category.getGuidance())) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, formatReason("No guidance provided", index));
        }
    }

    private void validateCategoryRule(CategoryRule categoryRule, Integer index) {
        Matcher matcher = categoryRule.getMatcher();
        if (matcher == null) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, formatReason("No matcher provided", index));
        }
        try {
            matcher.isMatch(new Result());
        } catch (Exception e) {
            String reason = String.format("Failed to evaluate matcher (%s)", e.getMessage());
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, formatReason(reason, index));
        }
    }

    private void validateCategorySnapshot(CategorySnapshot categorySnapshot, Integer index) {
        CategoryRule categoryRule = categorySnapshot.toCategoryRule();
        validateCategory(categoryRule.getCategory(), index);
        validateCategoryRule(categoryRule, index);
    }

    @Operation(summary = "Creates or updates categories using classpath resources")
    @PostMapping("/$initialize")
    public void initializeCategories() {
        try {
            categorizationService.initializeCategories();
        } catch (Exception e) {
            throw new ServerErrorException("Failed to initialize categories", e);
        }
    }

    @Operation(summary = "Imports categories and rules")
    @PostMapping("/$bulk-import")
    @Transactional
    public void bulkImportCategories(@RequestBody List<CategorySnapshot> categorySnapshots) {
        if (CollectionUtils.isEmpty(categorySnapshots)) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "No categories provided");
        }
        for (int index = 0; index < categorySnapshots.size(); index++) {
            validateCategorySnapshot(categorySnapshots.get(index), index);
        }
        List<String> duplicateIds = categorySnapshots.stream()
                .collect(Collectors.groupingBy(CategorySnapshot::getId, Collectors.counting()))
                .entrySet().stream()
                .filter(entry -> entry.getValue() > 1)
                .map(Map.Entry::getKey)
                .toList();
        if (!duplicateIds.isEmpty()) {
            throw new ResponseStatusException(
                    HttpStatus.BAD_REQUEST,
                    String.format("Duplicate IDs provided: %s", String.join(", ", duplicateIds)));
        }
        for (CategorySnapshot categorySnapshot : categorySnapshots) {
            categorizationService.saveCategorySnapshot(categorySnapshot);
        }
    }

    @Operation(summary = "Exports categories and rules")
    @GetMapping("/$bulk-export")
    public List<CategorySnapshot> bulkExportCategories() {
        return categoryRepository.findAll().stream()
                .map(CategorySnapshot::new)
                .toList();
    }

    @Operation(summary = "Gets all categories")
    @GetMapping
    public List<Category> getCategories() {
        return categoryRepository.findAll();
    }

    @Operation(summary = "Gets a category")
    @GetMapping("/{id}")
    public Category getCategory(@PathVariable String id) {
        return categoryRepository.findById(id)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "Category not found"));
    }

    @Operation(summary = "Gets the latest rule for a category", description = "Returns the rule that has the most recent `timestamp`.")
    @GetMapping("/{id}/rule")
    public CategoryRule getLatestCategoryRule(@PathVariable String id) {
        CategoryRule categoryRule = getCategory(id).getLatestRule();
        if (categoryRule == null) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "Category rule not found");
        }
        return categoryRule;
    }

    @Operation(summary = "Gets all historical rules for a category", description = "The rule with the most recent timestamp is the considered the \"latest\" rule.")
    @GetMapping("/{id}/rule/history")
    public List<CategoryRule> getCategoryRules(@PathVariable String id) {
        return getCategory(id).getRules().stream()
                .sorted(Comparator.comparing(CategoryRule::getTimestamp).reversed())
                .toList();
    }

    @Operation(summary = "Creates or updates a category")
    @PutMapping("/{id}")
    public void saveCategory(@PathVariable String id, @RequestBody Category category) {
        validateCategory(category, null);
        if (!StringUtils.equals(category.getId(), id)) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "ID does not match URL");
        }
        categoryRepository.save(category);
    }

    @Operation(summary = "Creates a rule for a category", description = "Creating a rule for a category automatically makes it the `latest` rule.")
    @PutMapping("/{id}/rule")
    public void saveCategoryRule(@PathVariable String id, @RequestBody Matcher matcher) {
        Category category = getCategory(id);
        CategoryRule categoryRule = new CategoryRule();
        categoryRule.setCategory(category);
        categoryRule.setMatcher(matcher);
        validateCategoryRule(categoryRule, null);
        categoryRuleRepository.save(categoryRule);
    }

    @Operation(summary = "Deletes a category")
    @DeleteMapping("/{id}")
    @Transactional
    public void deleteCategory(@PathVariable String id) {
        categoryRuleRepository.deleteByCategoryId(id);
        categoryRepository.deleteById(id);
    }

    @Operation(
            summary = "Deletes a category rule",
            parameters = {
                    @Parameter(name = "ruleId", description = "The internal auto-incrementing id of the category's rule. This id can be found in the results of \"Gets the latest rule for a category\"")
            },
            description = "This should primarily be used to delete a rule that was entered in error. The `ruleId` may be the latest or it could be a historical rule. The primary use-case is to delete the latest, which was entered in error."
    )
    @DeleteMapping("/{ruleId}")
    public void deleteCategoryRule(@PathVariable long ruleId) {
        categoryRuleRepository.deleteById(ruleId);
    }
}
