package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;

@Repository
public interface PatientReportingEvaluationStatusRepository
        extends MongoRepository<PatientReportingEvaluationStatus, String>,
        PatientReportingEvaluationStatusUpsertingRepository {
    Optional<PatientReportingEvaluationStatus> findByFacilityIdAndCorrelationId(String facilityId, String correlationId);

    Optional<PatientReportingEvaluationStatus> findByFacilityIdAndPatientIdAndReportsReportTrackingId(String facilityId, String patientId, String reportTrackingId);
}
