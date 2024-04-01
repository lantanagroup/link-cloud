package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.ValidationResult;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface ValidationResultRepository extends JpaRepository<ValidationResult, Long> {
    /*
    @Query("SELECT v FROM validationResult v WHERE v.tenantId = :tenantId AND v.reportId = :reportId")
    List<ValidationResult> findByTenantIdAndReportId(String tenantId, String reportId);
     */
}
