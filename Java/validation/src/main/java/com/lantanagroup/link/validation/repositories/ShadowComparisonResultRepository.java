package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.ShadowComparisonResult;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

@Repository
public interface ShadowComparisonResultRepository extends JpaRepository<ShadowComparisonResult, UUID> {
    List<ShadowComparisonResult> findByComparedAtBetween(OffsetDateTime start, OffsetDateTime end);

    List<ShadowComparisonResult> findByRequestIdOrderByComparedAtDesc(UUID requestId);
}
