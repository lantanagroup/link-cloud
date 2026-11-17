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

@Repository
public interface RubricVersionRepository extends JpaRepository<RubricVersion, Long> {

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
    int recordDryRun(@Param("rubricVersionId") Long rubricVersionId,
                     @Param("status") RubricResultStatus status,
                     @Param("completedAt") OffsetDateTime completedAt);

    // guarded DRAFT->PUBLISHED: keep the status check in the WHERE so it runs under the update's
    // row lock. two concurrent publishes can't both win — the first flips the row (1), the rest
    // see it's no longer DRAFT and touch nothing (0). returns rows affected.
    @Transactional
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    // the checksum is guarded too, so the definition published is the one the caller checked (its dry run);
    // a draft replaced in between changes the checksum and this touches nothing
    @Query("update RubricVersion v set v.status = :target, v.publishedAt = :publishedAt, v.publishedBy = :publishedBy "
            + "where v.rubricVersionId = :rubricVersionId and v.status = :expected and v.checksum = :checksum")
    int publishIfStatus(@Param("rubricVersionId") Long rubricVersionId,
                        @Param("expected") RubricVersionStatus expected,
                        @Param("checksum") String checksum,
                        @Param("target") RubricVersionStatus target,
                        @Param("publishedAt") OffsetDateTime publishedAt,
                        @Param("publishedBy") String publishedBy);

    // guarded draft replacement, like publish: the status (and, when the caller sent If-Match, the checksum it
    // read) is checked under the update's row lock, so a save made on a stale read touches nothing (0 rows)
    // instead of writing a DRAFT back over a version published in between, or over another person's save.
    // The dry run is cleared only when the definition changes; the CASE reads the row's checksum before the
    // update, so a dry run recorded since the caller's read survives a save that changes nothing.
    @Transactional
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("update RubricVersion v set v.checksum = :checksum, v.definitionJson = :definitionJson, "
            + "v.dimensionsJson = :dimensionsJson, v.applicableContextJson = :applicableContextJson, "
            + "v.scoringPolicyJson = :scoringPolicyJson, v.createdBy = :createdBy, "
            + "v.dryRunCompletedAt = case when v.checksum = :checksum then v.dryRunCompletedAt else null end, "
            + "v.dryRunStatus = case when v.checksum = :checksum then v.dryRunStatus else null end "
            + "where v.rubricVersionId = :rubricVersionId and v.status = :draft "
            + "and (:expectedChecksum is null or v.checksum = :expectedChecksum)")
    int replaceDraftIfUnchanged(@Param("rubricVersionId") Long rubricVersionId,
                                @Param("draft") RubricVersionStatus draft,
                                @Param("expectedChecksum") String expectedChecksum,
                                @Param("checksum") String checksum,
                                @Param("definitionJson") String definitionJson,
                                @Param("dimensionsJson") String dimensionsJson,
                                @Param("applicableContextJson") String applicableContextJson,
                                @Param("scoringPolicyJson") String scoringPolicyJson,
                                @Param("createdBy") String createdBy);

    // same trick for retire: status <> RETIRED makes a double retire a no-op (0 rows) so we don't
    // overwrite retiredAt/retiredBy or record a second event
    @Transactional
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("update RubricVersion v set v.status = :retired, v.retiredAt = :retiredAt, v.retiredBy = :retiredBy "
            + "where v.rubricVersionId = :rubricVersionId and v.status <> :retired")
    int retireIfNotRetired(@Param("rubricVersionId") Long rubricVersionId,
                           @Param("retired") RubricVersionStatus retired,
                           @Param("retiredAt") OffsetDateTime retiredAt,
                           @Param("retiredBy") String retiredBy);

    // guarded delete of a draft: the status check runs under the delete's row lock, so a version that
    // was published or retired between the caller's read and here is left alone (0 rows)
    @Transactional
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from RubricVersion v where v.rubricVersionId = :rubricVersionId and v.status = :expected")
    int deleteIfStatus(@Param("rubricVersionId") Long rubricVersionId,
                       @Param("expected") RubricVersionStatus expected);

    // for a delete made on condition that the draft is the one the caller read: a draft saved in between
    // changes the checksum and this touches nothing
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from RubricVersion v where v.rubricVersionId = :rubricVersionId and v.status = :expected "
            + "and v.checksum = :checksum")
    int deleteIfStatusAndChecksum(@Param("rubricVersionId") Long rubricVersionId,
                                  @Param("expected") RubricVersionStatus expected,
                                  @Param("checksum") String checksum);

    long countByRubricId(String rubricId);

    // a count, not entities, so it reads the database and not versions this transaction already loaded
    long countByRubricIdAndStatusAndRubricVersionIdNot(String rubricId, RubricVersionStatus status, Long rubricVersionId);
}
