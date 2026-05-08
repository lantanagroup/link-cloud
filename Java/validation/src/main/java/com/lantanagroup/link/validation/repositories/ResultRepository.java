package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Result;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface ResultRepository extends JpaRepository<Result, Long> {
    long deleteByFacilityId(String facilityId);

    long deleteByFacilityIdAndReportId(String facilityId, String reportId);

    long deleteByFacilityIdAndReportIdAndPatientId(String facilityId, String reportId, String patientId);

    List<Result> findAllByFacilityIdAndReportId(String facilityId, String reportId);

    List<Result> findAllByFacilityIdAndReportIdAndPatientId(String facilityId, String reportId, String patientId);
}
