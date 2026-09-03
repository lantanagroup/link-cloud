package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.LegacyShadowResult;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;
import java.util.UUID;

@Repository
public interface LegacyShadowResultRepository extends JpaRepository<LegacyShadowResult, UUID> {
    Optional<LegacyShadowResult> findFirstByRequestIdOrderByRequestedAtDesc(UUID requestId);
}
