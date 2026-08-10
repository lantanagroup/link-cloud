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

    List<RubricVersion> findByRubricIdInAndStatus(Collection<String> rubricIds, RubricVersionStatus status);

    // only touch the dry-run columns, a full entity save could clobber a concurrent publish/retire
    @Transactional
    @Modifying
    @Query("update RubricVersion v set v.dryRunCompletedAt = :completedAt, v.dryRunStatus = :status "
            + "where v.rubricVersionId = :rubricVersionId")
    int recordDryRun(@Param("rubricVersionId") UUID rubricVersionId,
                     @Param("status") RubricResultStatus status,
                     @Param("completedAt") OffsetDateTime completedAt);

    // guarded DRAFT->PUBLISHED: keep the status check in the WHERE so it runs under the update's
    // row lock. two concurrent publishes can't both win — the first flips the row (1), the rest
    // see it's no longer DRAFT and touch nothing (0). returns rows affected.
    @Transactional
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("update RubricVersion v set v.status = :target, v.publishedAt = :publishedAt, v.publishedBy = :publishedBy "
            + "where v.rubricVersionId = :rubricVersionId and v.status = :expected")
    int publishIfStatus(@Param("rubricVersionId") UUID rubricVersionId,
                        @Param("expected") RubricVersionStatus expected,
                        @Param("target") RubricVersionStatus target,
                        @Param("publishedAt") OffsetDateTime publishedAt,
                        @Param("publishedBy") String publishedBy);

    // same trick for retire: status <> RETIRED makes a double retire a no-op (0 rows) so we don't
    // overwrite retiredAt/retiredBy or record a second event
    @Transactional
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("update RubricVersion v set v.status = :retired, v.retiredAt = :retiredAt, v.retiredBy = :retiredBy "
            + "where v.rubricVersionId = :rubricVersionId and v.status <> :retired")
    int retireIfNotRetired(@Param("rubricVersionId") UUID rubricVersionId,
                           @Param("retired") RubricVersionStatus retired,
                           @Param("retiredAt") OffsetDateTime retiredAt,
                           @Param("retiredBy") String retiredBy);
}
