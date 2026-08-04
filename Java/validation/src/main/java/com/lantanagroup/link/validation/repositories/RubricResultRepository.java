package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.RubricResult;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;
import java.util.UUID;

@Repository
public interface RubricResultRepository extends JpaRepository<RubricResult, UUID> {

    Optional<RubricResult> findByRequestId(UUID requestId);
}
