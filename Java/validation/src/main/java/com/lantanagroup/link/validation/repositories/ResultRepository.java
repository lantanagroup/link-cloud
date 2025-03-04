package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Result;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface ResultRepository extends JpaRepository<Result, Long> {
    long deleteByFacilityId(String facilityId);

    long deleteByFacilityIdAndReportId(String facilityId, String reportId);

    long deleteByFacilityIdAndReportIdAndPatientId(String facilityId, String reportId, String patientId);
}
