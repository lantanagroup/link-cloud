package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.CategoryRuleSetsEntity;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;

import java.util.List;

public interface CategoryRuleSetsRepository extends JpaRepository<CategoryRuleSetsEntity, String> {
    List<CategoryRuleSetsEntity> findByCategoryId(String categoryId);

    @Query(value = "SELECT TOP 1 * FROM category_rulesets WHERE category_id = :categoryId ORDER BY version DESC", nativeQuery = true)
    CategoryRuleSetsEntity getLatestCategoryRules(String categoryId);

    void deleteByCategoryId(String categoryId);
}
