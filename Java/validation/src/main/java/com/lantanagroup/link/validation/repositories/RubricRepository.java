package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import jakarta.persistence.LockModeType;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.JpaSpecificationExecutor;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

@Repository
public interface RubricRepository extends JpaRepository<Rubric, String>, JpaSpecificationExecutor<Rubric> {

    // rubrics that have at least one version in the given status, paged like findAll
    @Query("select r from Rubric r where exists (select 1 from RubricVersion v "
            + "where v.rubricId = r.rubricId and v.status = :status)")
    Page<Rubric> findByVersionStatus(@Param("status") RubricVersionStatus status, Pageable pageable);

    // every owner a rubric has, for the list's owner filter. rubrics without one are left out
    @Query("select distinct r.owner from Rubric r where r.owner is not null and r.owner <> '' order by r.owner")
    List<String> findDistinctOwners();

    // locks the rubric's row until the transaction ends, so lifecycle changes that depend on its other
    // versions (retiring the pipeline rubric's last published one) run one at a time
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("select r from Rubric r where r.rubricId = :rubricId")
    Optional<Rubric> lockById(@Param("rubricId") String rubricId);
}
