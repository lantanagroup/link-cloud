package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.AbstractResourceEntity;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.PatientResource;
import com.lantanagroup.link.measureeval.entities.SharedResource;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
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

    public List<SharedResource> findSharedResources(String facilityId, List<PatientReportingEvaluationStatus.Resource> resources) {
        List<SharedResource> sharedResources = new ArrayList<>();
        if (resources == null || resources.isEmpty()) {
            return sharedResources;
        }

        // Batch the searches so that it doesn't exceed mongo's query limitations
        List<PatientReportingEvaluationStatus.Resource> remainingResources = new ArrayList<>(resources);
        while (!remainingResources.isEmpty()) {
            int characterCount = 0;
            List<Criteria> batchCriteria = new ArrayList<>();

            for (int i = remainingResources.size() - 1; i >= 0; i--) {
                PatientReportingEvaluationStatus.Resource resource = remainingResources.get(i);
                int resourceCharLength = resource.getResourceType().toString().length() + resource.getResourceId().length();

                if (characterCount + resourceCharLength >= 10000) {
                    break;
                }

                characterCount += resourceCharLength;
                batchCriteria.add(Criteria.where("facilityId").is(facilityId)
                        .and("resourceType").is(resource.getResourceType())
                        .and("resourceId").is(resource.getResourceId()));
                remainingResources.remove(i);
            }

            if (!batchCriteria.isEmpty()) {
                Criteria combinedCriteria = new Criteria().orOperator(batchCriteria.toArray(new Criteria[0]));
                Query query = new Query(combinedCriteria);
                sharedResources.addAll(mongoOperations.find(query, SharedResource.class));
            }
        }

        return sharedResources;
    }

    public List<PatientResource> findPatientResources(String facilityId, String patientId, List<String> reportIds) {
        List<AggregationOperation> pipeline = new ArrayList<>();

        pipeline.add(Aggregation.match(Criteria.where("facilityId").is(facilityId)
                .and("patientId").is(patientId)
                .and("reports.reportTrackingId").in(reportIds)));
        pipeline.add(Aggregation.unwind("resources"));
        pipeline.add(Aggregation.match(Criteria.where("resources.normalizationStatus").is("NORMALIZED")));
        pipeline.add(Aggregation.lookup("patientResource", "resources.resourceId", "resourceId", "matchedResources"));
        pipeline.add(Aggregation.unwind("matchedResources"));
        pipeline.add(Aggregation.match(Criteria.where("matchedResources.facilityId").is(facilityId)));
        pipeline.add(Aggregation.replaceRoot("matchedResources"));

        Aggregation aggregation = Aggregation.newAggregation(pipeline);
        return mongoOperations.aggregate(aggregation, "patientReportingEvaluationStatus", PatientResource.class).getMappedResults();
    }
}
