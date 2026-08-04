package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.FacilityOverride;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

@Repository
public interface FacilityOverrideRepository extends JpaRepository<FacilityOverride, UUID> {

    @Query("""
           SELECT fo
             FROM FacilityOverride fo
            WHERE fo.facilityId = :facilityId
              AND fo.rubricId   = :rubricId
              AND fo.effectiveFrom <= :asOf
              AND (fo.effectiveTo IS NULL OR fo.effectiveTo > :asOf)
              AND (fo.rubricVersionId IS NULL OR fo.rubricVersionId = :rubricVersionId)
            ORDER BY fo.effectiveFrom DESC
           """)
    List<FacilityOverride> findActive(
            @Param("facilityId") String facilityId,
            @Param("rubricId") String rubricId,
            @Param("rubricVersionId") UUID rubricVersionId,
            @Param("asOf") OffsetDateTime asOf);

    List<FacilityOverride> findByFacilityIdOrderByCreatedAtDesc(String facilityId);
}
