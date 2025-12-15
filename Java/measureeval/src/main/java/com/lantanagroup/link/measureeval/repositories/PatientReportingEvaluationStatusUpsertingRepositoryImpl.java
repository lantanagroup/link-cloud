package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import org.springframework.data.mongodb.core.FindAndModifyOptions;
import org.springframework.data.mongodb.core.MongoOperations;

import static org.springframework.data.mongodb.core.query.Criteria.where;
import static org.springframework.data.mongodb.core.query.Query.query;
import static org.springframework.data.mongodb.core.query.Update.update;

public class PatientReportingEvaluationStatusUpsertingRepositoryImpl implements PatientReportingEvaluationStatusUpsertingRepository {
    private final MongoOperations mongoOperations;

    public PatientReportingEvaluationStatusUpsertingRepositoryImpl(MongoOperations mongoOperations) {
        this.mongoOperations = mongoOperations;
    }

    @Override
    public PatientReportingEvaluationStatus setPatientId(String facilityId, String correlationId, String patientId) {
        return mongoOperations.update(PatientReportingEvaluationStatus.class)
                .matching(query(where("facilityId").is(facilityId).and("correlationId").is(correlationId)))
                .apply(update("patientId", patientId))
                .withOptions(FindAndModifyOptions.options().returnNew(true))
                .findAndModifyValue();
    }
}
