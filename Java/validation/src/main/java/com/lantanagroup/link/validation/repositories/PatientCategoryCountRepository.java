package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.PatientCategoryCount;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface PatientCategoryCountRepository extends JpaRepository<PatientCategoryCount, Long> {
    List<PatientCategoryCount> findAllByFacilityIdAndReportIdAndPatientId(
            String facilityId,
            String reportId,
            String patientId);
}
