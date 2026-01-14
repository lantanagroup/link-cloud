package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.Resource;
import org.springframework.data.mongodb.core.FindAndReplaceOptions;
import org.springframework.data.mongodb.core.MongoOperations;

import static org.springframework.data.mongodb.core.query.Criteria.where;
import static org.springframework.data.mongodb.core.query.Query.query;

public class ResourceUpsertingRepositoryImpl implements ResourceUpsertingRepository {
    private final MongoOperations mongoOperations;

    public ResourceUpsertingRepositoryImpl(MongoOperations mongoOperations) {
        this.mongoOperations = mongoOperations;
    }

    @Override
    public Resource upsert(Resource entity) {
        return mongoOperations.update(Resource.class)
                .matching(query(where("facilityId").is(entity.getFacilityId())
                        .and("correlationId").is(entity.getCorrelationId())
                        .and("resourceType").is(entity.getResourceType())
                        .and("resourceId").is(entity.getResourceId())))
                .replaceWith(entity)
                .withOptions(FindAndReplaceOptions.options().upsert().returnNew())
                .findAndReplaceValue();
    }
}
