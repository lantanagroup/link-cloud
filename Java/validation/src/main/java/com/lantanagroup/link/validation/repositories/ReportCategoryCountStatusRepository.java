package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.ReportCategoryCountStatus;
import jakarta.persistence.LockModeType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.stereotype.Repository;

import java.util.Optional;

@Repository
public interface ReportCategoryCountStatusRepository extends JpaRepository<ReportCategoryCountStatus, Long> {
    Optional<ReportCategoryCountStatus> findByFacilityIdAndReportId(String facilityId, String reportId);

    /**
     * Reads the report's status row and holds a write lock on it until the transaction ends, so only one patient's
     * counts are applied to the report's totals at a time.
     */
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<ReportCategoryCountStatus> findWithLockByFacilityIdAndReportId(String facilityId, String reportId);
}
