package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.AbstractResourceEntity;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.PatientResource;
import com.lantanagroup.link.measureeval.entities.SharedResource;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
import org.hl7.fhir.r4.model.ResourceType;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.stereotype.Repository;

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
}
