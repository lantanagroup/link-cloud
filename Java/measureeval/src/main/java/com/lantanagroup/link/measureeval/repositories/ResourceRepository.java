package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.Resource;
import org.springframework.data.mongodb.core.FindAndReplaceOptions;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.stereotype.Repository;

import java.util.List;

import static org.springframework.data.mongodb.core.query.Criteria.byExample;

@Repository
public class ResourceRepository {
    private final MongoOperations mongoOperations;

    public ResourceRepository(MongoOperations mongoOperations) {
        this.mongoOperations = mongoOperations;
    }

    public List<Resource> findAll(String facilityId, String correlationId) {
        Resource probe = new Resource();
        probe.setFacilityId(facilityId);
        probe.setCorrelationId(correlationId);
        return mongoOperations.query(Resource.class)
                .matching(byExample(probe))
                .all();
    }

    public Resource upsert(Resource entity) {
        Resource probe = new Resource();
        probe.setFacilityId(entity.getFacilityId());
        probe.setCorrelationId(entity.getCorrelationId());
        probe.setResourceType(entity.getResourceType());
        probe.setResourceId(entity.getResourceId());
        return mongoOperations.update(Resource.class)
                .matching(byExample(probe))
                .replaceWith(entity)
                .withOptions(FindAndReplaceOptions.options().upsert().returnNew())
                .findAndReplaceValue();
    }
}
