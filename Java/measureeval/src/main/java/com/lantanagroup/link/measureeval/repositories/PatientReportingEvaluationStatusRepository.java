package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import org.springframework.data.domain.Example;
import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

@Repository
public interface PatientReportingEvaluationStatusRepository
        extends MongoRepository<PatientReportingEvaluationStatus, String> {
    default Optional<PatientReportingEvaluationStatus> findOne(String facilityId, String correlationId) {
        PatientReportingEvaluationStatus probe = new PatientReportingEvaluationStatus();
        probe.setFacilityId(facilityId);
        probe.setCorrelationId(correlationId);
        probe.setReports(null);
        probe.setResources(null);

        return findOne(Example.of(probe));
    }

    default Optional<PatientReportingEvaluationStatus> findOne(String facilityId, String patientId, String reportTrackingID) {
        PatientReportingEvaluationStatus probe = new PatientReportingEvaluationStatus();
        probe.setFacilityId(facilityId);
        probe.setPatientId(patientId);
        PatientReportingEvaluationStatus.Report reportProbe = new PatientReportingEvaluationStatus.Report();
        reportProbe.setReportTrackingId(reportTrackingID);
        probe.setReports(List.of(reportProbe));
        probe.setResources(null);
        probe.setCorrelationId(null);

        return findOne(Example.of(probe));
    }
}
