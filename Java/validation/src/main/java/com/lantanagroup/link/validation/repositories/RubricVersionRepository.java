package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

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
}
