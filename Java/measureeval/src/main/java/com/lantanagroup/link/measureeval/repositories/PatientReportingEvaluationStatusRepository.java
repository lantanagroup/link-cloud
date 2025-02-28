package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import org.springframework.data.domain.Example;
import org.springframework.data.domain.ExampleMatcher;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.data.mongodb.core.query.Criteria;
//import org.springframework.data.mongodb.core.query.Query;
import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.data.mongodb.repository.Query;
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
}
