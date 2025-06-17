package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.CategoryRule;
import org.springframework.data.jpa.repository.JpaRepository;

public interface CategoryRuleRepository extends JpaRepository<CategoryRule, Long> {
    long deleteByCategoryId(String categoryId);
}
