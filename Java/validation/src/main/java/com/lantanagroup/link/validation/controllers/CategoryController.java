package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.entities.CategoryEntity;
import com.lantanagroup.link.validation.entities.CategoryRuleSetsEntity;
import com.lantanagroup.link.validation.model.BulkSaveCategory;
import com.lantanagroup.link.validation.model.CategoryRuleSetsModel;
import com.lantanagroup.link.validation.model.LatestCategoryRuleSetsModel;
import com.lantanagroup.link.validation.repositories.CategoryRepository;
import com.lantanagroup.link.validation.repositories.CategoryRuleSetsRepository;
import io.swagger.v3.oas.annotations.Operation;
import jakarta.transaction.Transactional;
import org.apache.commons.lang3.StringUtils;
import org.slf4j.Logger;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;
import java.util.stream.Collectors;

@RestController
@RequestMapping("/api/category")
public class CategoryController {
    private static final Logger log = org.slf4j.LoggerFactory.getLogger(CategoryController.class);

    private final CategoryRepository categoryRepository;
    private final CategoryRuleSetsRepository categoryRuleSetsRepository;

    public CategoryController(CategoryRepository categoryRepository, CategoryRuleSetsRepository categoryRuleSetsRepository) {
        this.categoryRepository = categoryRepository;
        this.categoryRuleSetsRepository = categoryRuleSetsRepository;
    }

    @Operation(summary = "Create or update a category")
    @PostMapping
    public void createOrUpdateCategory(@RequestBody CategoryEntity category) {
        if (StringUtils.isEmpty(category.getId())) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Category ID is required");
        }

        if (StringUtils.isEmpty(category.getGuidance())) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Category guidance is required");
        }

        log.info("Creating/updating category with ID: {}", category.getId());

        this.categoryRepository.save(category);
    }

    @Operation(summary = "Get all categories")
    @GetMapping
    public List<CategoryEntity> getCategories() {
        return this.categoryRepository.findAll()
                .stream().peek(category -> category.setRequireAllRuleSets(null))
                .toList();
    }

    @Operation(summary = "Get the latest version of rules for a category by ID")
    @GetMapping("/{categoryId}/rules")
    public LatestCategoryRuleSetsModel getCategoryRules(@PathVariable String categoryId) {
        CategoryRuleSetsEntity entity = this.categoryRuleSetsRepository.getLatestCategoryRules(categoryId);
        LatestCategoryRuleSetsModel model = null;

        if (entity != null) {
            model = new LatestCategoryRuleSetsModel();
            model.setVersion(entity.getVersion());
            model.setTimestamp(entity.getCreated());
            model.setRequireAllRuleSets(entity.isRequireAllRuleSets());
            model.setRuleSets(entity.getRuleSets());
        }

        return model;
    }

    @Operation(summary = "Create or update rule sets for a category by ID")
    @PostMapping("/{categoryId}/rules")
    public void createOrUpdateCategoryRules(@PathVariable String categoryId, @RequestBody CategoryRuleSetsModel categoryRuleSets) {
        if (StringUtils.isEmpty(categoryId)) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Category ID is required");
        }

        if (categoryRuleSets == null) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Category rules are required");
        }

        if (categoryRuleSets.getRuleSets() == null || categoryRuleSets.getRuleSets().isEmpty()) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Category rule sets are required");
        }

        log.info("Creating/updating rule sets for category with ID: {}", categoryId);

        CategoryEntity category = this.categoryRepository.findById(categoryId)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "Category not found"));

        CategoryRuleSetsEntity entity = new CategoryRuleSetsEntity();
        entity.setCategory(category);
        entity.setRequireAllRuleSets(categoryRuleSets.isRequireAllRuleSets());
        entity.setRuleSets(categoryRuleSets.getRuleSets());

        // Set the version of the new category rule set
        CategoryRuleSetsEntity foundCategoryRules = this.categoryRuleSetsRepository.getLatestCategoryRules(categoryId);
        entity.setVersion(foundCategoryRules != null ? foundCategoryRules.getVersion() + 1 : 1);

        this.categoryRuleSetsRepository.save(entity);       // Always save a new category rule set version
    }

    @Operation(summary = "Get the history of rule sets for a category by ID")
    @GetMapping("/{categoryId}/rules/history")
    public List<LatestCategoryRuleSetsModel> getCategoryRulesHistory(@PathVariable String categoryId) {
        return this.categoryRuleSetsRepository.findByCategoryId(categoryId)
                .stream().map(entity -> {
                    LatestCategoryRuleSetsModel model = new LatestCategoryRuleSetsModel();
                    model.setVersion(entity.getVersion());
                    model.setTimestamp(entity.getCreated());
                    model.setRequireAllRuleSets(entity.isRequireAllRuleSets());
                    model.setRuleSets(entity.getRuleSets());
                    return model;
                })
                .sorted((a, b) -> b.getVersion() - a.getVersion())
                .toList();
    }

    @Operation(summary = "Bulk save categories and their rule sets")
    @PostMapping("/bulk")
    @Transactional
    public void bulkSaveCategories(@RequestBody List<BulkSaveCategory> categories) {
        if (categories == null || categories.isEmpty()) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Categories are required");
        } else if (categories.stream().anyMatch(category -> StringUtils.isEmpty(category.getId()))) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "ID is required for all categories");
        }

        boolean hasDuplicateIds = categories.stream()
                .collect(Collectors.groupingBy(BulkSaveCategory::getId))
                .values()
                .stream()
                .anyMatch(ids -> ids.size() > 1);

        if (hasDuplicateIds) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Duplicate category IDs are not allowed");
        }

        if (categories.stream().anyMatch(category -> category.getRuleSets() == null || category.getRuleSets().isEmpty())) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Rule sets are required for all categories");
        }

        log.info("Bulk saving {} categories", categories.size());

        for (BulkSaveCategory category : categories) {
            CategoryEntity categoryEntity = new CategoryEntity();
            categoryEntity.setId(category.getId());
            categoryEntity.setAcceptable(category.isAcceptable());
            categoryEntity.setGuidance(category.getGuidance());
            categoryEntity.setRequireAllRuleSets(category.isRequireAllRuleSets());
            this.categoryRepository.save(categoryEntity);

            if (category.getRuleSets() != null) {
                CategoryRuleSetsEntity categoryRules = new CategoryRuleSetsEntity();
                categoryRules.setCategory(categoryEntity);
                categoryRules.setRuleSets(category.getRuleSets());

                // Set the version of the new category rule set
                CategoryRuleSetsEntity foundCategoryRules = this.categoryRuleSetsRepository.getLatestCategoryRules(category.getId());
                categoryRules.setVersion(foundCategoryRules != null ? foundCategoryRules.getVersion() + 1 : 1);

                this.categoryRuleSetsRepository.save(categoryRules);
            }
        }
    }

    @Operation(summary = "Delete a category by ID")
    @DeleteMapping("/{categoryId}")
    @Transactional
    public void deleteCategory(@PathVariable String categoryId) {
        log.info("Deleting category with ID: {}", categoryId);
        this.categoryRuleSetsRepository.deleteByCategoryId(categoryId);
        this.categoryRepository.deleteById(categoryId);
    }
}
