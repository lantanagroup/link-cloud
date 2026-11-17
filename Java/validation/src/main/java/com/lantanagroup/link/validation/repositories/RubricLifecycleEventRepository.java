package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.RubricLifecycleEvent;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface RubricLifecycleEventRepository extends JpaRepository<RubricLifecycleEvent, UUID> {

    List<RubricLifecycleEvent> findByRubricIdOrderByOccurredAtDesc(String rubricId);
}
