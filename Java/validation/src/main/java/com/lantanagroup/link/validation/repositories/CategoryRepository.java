package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Category;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.Collection;

@Repository
public interface CategoryRepository extends JpaRepository<Category, String> {
    void deleteByIdIn(Collection<String> ids);
}
