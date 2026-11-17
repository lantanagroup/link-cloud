package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.LegacyShadowFinding;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface LegacyShadowFindingRepository extends JpaRepository<LegacyShadowFinding, UUID> {
    List<LegacyShadowFinding> findByRequestId(UUID requestId);
}
