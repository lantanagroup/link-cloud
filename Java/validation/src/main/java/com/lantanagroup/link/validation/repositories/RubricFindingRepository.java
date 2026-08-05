package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.RubricFinding;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface RubricFindingRepository extends JpaRepository<RubricFinding, UUID> {

    List<RubricFinding> findByResultId(UUID resultId);
}
