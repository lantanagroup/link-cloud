package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.ReportCategoryCount;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface ReportCategoryCountRepository extends JpaRepository<ReportCategoryCount, Long> {
    List<ReportCategoryCount> findAllByFacilityIdAndReportId(String facilityId, String reportId);
}
