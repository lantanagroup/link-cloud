package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Result;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
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

    @Modifying
    @Query(value = "DELETE FROM result_category WHERE result_id IN (SELECT id FROM result"
            + " WHERE facility_id = :facilityId AND report_id = :reportId AND patient_id = :patientId)",
            nativeQuery = true)
    void deletePatientResultCategories(@Param("facilityId") String facilityId,
                                       @Param("reportId") String reportId,
                                       @Param("patientId") String patientId);

    @Modifying
    @Query(value = "DELETE FROM result"
            + " WHERE facility_id = :facilityId AND report_id = :reportId AND patient_id = :patientId",
            nativeQuery = true)
    void deletePatientResults(@Param("facilityId") String facilityId,
                              @Param("reportId") String reportId,
                              @Param("patientId") String patientId);

    @Query("select r from Result r join r.categories c where r.facilityId = :facilityId "
            + "and r.reportId = :reportId and c.id = :categoryId")
    Page<Result> findCategoryResults(
            @Param("facilityId") String facilityId, @Param("reportId") String reportId,
            @Param("categoryId") String categoryId, Pageable pageable);

    @Query("select r from Result r where r.facilityId = :facilityId and r.reportId = :reportId "
            + "and r.categories is empty")
    Page<Result> findUncategorizedResults(
            @Param("facilityId") String facilityId, @Param("reportId") String reportId,
            Pageable pageable);

    List<Result> findAllByFacilityIdAndReportId(String facilityId, String reportId);

    List<Result> findAllByFacilityIdAndReportIdAndPatientId(String facilityId, String reportId, String patientId);

    long countByFacilityIdAndReportIdAndSeverityIn(
            String facilityId,
            String reportId,
            Collection<OperationOutcome.IssueSeverity> severities);
}
