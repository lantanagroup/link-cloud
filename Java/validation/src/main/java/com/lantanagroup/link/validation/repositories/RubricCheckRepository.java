package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.RubricCheck;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Repository
public interface RubricCheckRepository extends JpaRepository<RubricCheck, Long> {

    // live checks only, soft-deleted rows are hidden from evaluate/dry-run and the read APIs
    List<RubricCheck> findByRubricVersionIdAndDeletedFalseOrderByOrdinalAsc(Long rubricVersionId);

    // bulk update on purpose: it runs immediately, so the old rows are already flagged
    // before the replacement checks insert (otherwise the filtered unique index
    // uq_check_rv_local_active would reject reused local ids)
    @Transactional
    @Modifying
    @Query("update RubricCheck c set c.deleted = true "
            + "where c.rubricVersionId = :rubricVersionId and c.deleted = false")
    int softDeleteByRubricVersionId(@Param("rubricVersionId") Long rubricVersionId);
}
