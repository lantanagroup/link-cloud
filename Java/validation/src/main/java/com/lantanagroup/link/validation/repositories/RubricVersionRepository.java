package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.annotation.Transactional;

import java.time.OffsetDateTime;
import java.util.Collection;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

@Repository
public interface RubricVersionRepository extends JpaRepository<RubricVersion, UUID> {

    // Semver ordering is semantic, not lexical — callers sort with Semver.versionComparator()
    List<RubricVersion> findByRubricId(String rubricId);

    List<RubricVersion> findByRubricIdAndStatus(String rubricId, RubricVersionStatus status);

    Optional<RubricVersion> findByRubricIdAndSemver(String rubricId, String semver);

    // Batch-load versions for a page of rubrics (avoids N+1 in the rubric list endpoint)
    List<RubricVersion> findByRubricIdIn(Collection<String> rubricIds);

    // only touch the dry-run columns, a full entity save could clobber a concurrent publish/retire
    @Transactional
    @Modifying
    @Query("update RubricVersion v set v.dryRunCompletedAt = :completedAt, v.dryRunStatus = :status "
            + "where v.rubricVersionId = :rubricVersionId")
    int recordDryRun(@Param("rubricVersionId") UUID rubricVersionId,
                     @Param("status") RubricResultStatus status,
                     @Param("completedAt") OffsetDateTime completedAt);
}
