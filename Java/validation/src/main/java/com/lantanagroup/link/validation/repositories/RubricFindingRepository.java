package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.RubricFinding;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface RubricFindingRepository extends JpaRepository<RubricFinding, Long> {

    List<RubricFinding> findByResultId(Long resultId);
}
