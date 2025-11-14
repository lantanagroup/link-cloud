package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.AbstractResourceEntity;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.PatientResource;
import com.lantanagroup.link.measureeval.entities.SharedResource;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
import com.lantanagroup.link.shared.mongo.CustomAggregationOperation;
import org.bson.Document;
import org.hl7.fhir.r4.model.ResourceType;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.data.mongodb.core.aggregation.Aggregation;
import org.springframework.data.mongodb.core.aggregation.AggregationOperation;
import org.springframework.data.mongodb.core.query.Criteria;
import org.springframework.data.mongodb.core.query.Query;
import org.springframework.stereotype.Repository;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import static org.springframework.data.mongodb.core.query.Criteria.byExample;

@Repository
public class AbstractResourceRepository {
    private final MongoOperations mongoOperations;

    public AbstractResourceRepository(MongoOperations mongoOperations) {
        this.mongoOperations = mongoOperations;
    }

    public AbstractResourceEntity findOne(
            String facilityId,
            boolean isPatientResource,
            ResourceType resourceType,
            String resourceId) {
        Class<? extends AbstractResourceEntity> entityType;
        AbstractResourceEntity probe;
        if (isPatientResource) {
            entityType = PatientResource.class;
            probe = new PatientResource();
        } else {
            entityType = SharedResource.class;
            probe = new SharedResource();
        }
        probe.setFacilityId(facilityId);
        probe.setResourceType(resourceType);
        probe.setResourceId(resourceId);
        return mongoOperations.query(entityType)
                .matching(byExample(probe))
                .oneValue();
    }

    public AbstractResourceEntity findOne(String facilityId, AbstractResourceRecord source) {
        return findOne(facilityId, source.isPatientResource(), source.getResourceType(), source.getResourceId());
    }

    public AbstractResourceEntity findOne(String facilityId, PatientReportingEvaluationStatus.Resource source) {
        return findOne(facilityId, source.getIsPatientResource(), source.getResourceType(), source.getResourceId());
    }

    public <T extends AbstractResourceEntity> T save(T entity) {
        return mongoOperations.save(entity);
    }

    public List<? extends AbstractResourceEntity> findAll(String facilityId, List<PatientReportingEvaluationStatus.Resource> resources, Class<? extends AbstractResourceEntity> entityType) {
        if (resources == null || resources.isEmpty()) {
            return Collections.emptyList();
        }

        List<Criteria> criteriaList = resources.stream()
                .map(resource -> Criteria.where("facilityId").is(facilityId)
                        .and("resourceType").is(resource.getResourceType())
                        .and("resourceId").is(resource.getResourceId()))
                .toList();

        Criteria combinedCriteria = new Criteria().orOperator(criteriaList.toArray(new Criteria[0]));
        Query query = new Query(combinedCriteria);

        return mongoOperations.find(query, entityType);
    }

    public List<PatientResource> findResources(boolean isShared, String facilityId, String correlationId) {
        List<AggregationOperation> pipeline = new ArrayList<>();

        pipeline.add(Aggregation.match(Criteria.where("facilityId").is(facilityId)
                .and("correlationId").is(correlationId)));
        pipeline.add(Aggregation.unwind("resources"));
        pipeline.add(Aggregation.replaceRoot("resources"));
        pipeline.add(Aggregation.match(Criteria.where("isPatientResource").is(!isShared)
                .and("normalizationStatus").is("NORMALIZED")));

        Document lookupStage = new Document("$lookup",
                new Document("from", isShared ? "sharedResource" : "patientResource")
                        .append("localField", "resourceId")
                        .append("foreignField", "resourceId")
                        .append("as", "patientResources"));

        pipeline.add(new CustomAggregationOperation(lookupStage));
        pipeline.add(Aggregation.unwind("patientResources"));

        Document matchExpr = new Document("$match",
                new Document("$expr",
                        new Document("$and", List.of(
                                new Document("$eq", List.of("$patientResources.facilityId", facilityId)),
                                new Document("$eq", List.of("$patientResources.resourceType", "$resourceType"))
                        ))));
        pipeline.add(new CustomAggregationOperation(matchExpr));

        pipeline.add(Aggregation.replaceRoot("patientResources"));

        Aggregation aggregation = Aggregation.newAggregation(pipeline);
        return mongoOperations.aggregate(aggregation, "patientReportingEvaluationStatus", PatientResource.class).getMappedResults();
    }
}
