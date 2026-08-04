package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Rubric;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface RubricRepository extends JpaRepository<Rubric, String> {
}
