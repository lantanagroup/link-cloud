package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.RubricCheck;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface RubricCheckRepository extends JpaRepository<RubricCheck, UUID> {

    List<RubricCheck> findByRubricVersionIdOrderByOrdinalAsc(UUID rubricVersionId);
}
