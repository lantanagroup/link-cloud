package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.CategoryEntity;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface CategoryRepository extends JpaRepository<CategoryEntity, String> {
}
