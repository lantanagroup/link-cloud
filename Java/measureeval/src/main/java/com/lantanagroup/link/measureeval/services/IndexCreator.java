package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import org.springframework.context.event.ContextRefreshedEvent;
import org.springframework.context.event.EventListener;
import org.springframework.data.domain.Sort;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.data.mongodb.core.index.Index;
import org.springframework.stereotype.Component;

@Component
public class IndexCreator {
    private final MongoOperations mongoOperations;

    public IndexCreator(MongoOperations mongoOperations) {
        this.mongoOperations = mongoOperations;
    }

    @EventListener(ContextRefreshedEvent.class)
    public void contextRefreshed() {
        mongoOperations.indexOps(Resource.class).ensureIndex(new Index()
                .on("facilityId", Sort.Direction.ASC)
                .on("correlationId", Sort.Direction.ASC)
                .on("resourceType", Sort.Direction.ASC)
                .on("resourceId", Sort.Direction.ASC));
    }
}
