package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.RubricLifecycleEvent;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.UUID;

@Repository
public interface RubricLifecycleEventRepository extends JpaRepository<RubricLifecycleEvent, UUID> {

    List<RubricLifecycleEvent> findByRubricIdOrderByOccurredAtDesc(String rubricId);

    // a rubric's whole history, removed with the rubric when its last version is deleted
    @Transactional
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from RubricLifecycleEvent e where e.rubricId = :rubricId")
    int deleteByRubricId(@Param("rubricId") String rubricId);
}
