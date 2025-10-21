package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;

public interface PatientReportingEvaluationStatusUpsertingRepository {
    PatientReportingEvaluationStatus setPatientId(String facilityId, String correlationId, String patientId);

    default PatientReportingEvaluationStatus setPatientId(PatientReportingEvaluationStatus entity) {
        return setPatientId(entity.getFacilityId(), entity.getCorrelationId(), entity.getPatientId());
    }
}
