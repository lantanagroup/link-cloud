package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.data.mongodb.core.query.Criteria;
import org.springframework.data.mongodb.core.query.Query;
import org.springframework.stereotype.Service;

@Service
public class PatientReportingEvaluationStatusTemplateRepository {

    private final MongoTemplate mongoTemplate;

    public PatientReportingEvaluationStatusTemplateRepository(MongoTemplate mongoTemplate) {
        this.mongoTemplate = mongoTemplate;
    }

    public PatientReportingEvaluationStatus getFirstByFacilityIdAndPatientIdAndReports_ReportTrackingId(String facilityId, String patientId, String reportTrackingId) {
        Query query = new Query();
        query.addCriteria(Criteria.where("reports.reportTrackingId").is(reportTrackingId));
        query.addCriteria(Criteria.where("facilityId").is(facilityId));
        query.addCriteria(Criteria.where("patientId").is(patientId));
        return mongoTemplate.find(query, PatientReportingEvaluationStatus.class).stream().findFirst().orElse(null);
    }
}
