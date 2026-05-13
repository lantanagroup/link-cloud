package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Result;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.Collection;
import java.util.List;

@Repository
public interface ResultRepository extends JpaRepository<Result, Long> {

    @Modifying
    @Query(value = "DELETE FROM result_category WHERE category_id IN :categoryIds", nativeQuery = true)
    void deleteByCategoryIds(@Param("categoryIds") Collection<String> categoryIds);
    long deleteByFacilityId(String facilityId);

    long deleteByFacilityIdAndReportId(String facilityId, String reportId);

    long deleteByFacilityIdAndReportIdAndPatientId(String facilityId, String reportId, String patientId);

    List<Result> findAllByFacilityIdAndReportId(String facilityId, String reportId);

    List<Result> findAllByFacilityIdAndReportIdAndPatientId(String facilityId, String reportId, String patientId);
}
