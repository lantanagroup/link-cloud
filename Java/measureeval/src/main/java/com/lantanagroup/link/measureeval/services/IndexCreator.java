package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.context.event.ContextRefreshedEvent;
import org.springframework.context.event.EventListener;
import org.springframework.data.domain.Sort;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.data.mongodb.core.index.Index;
import org.springframework.stereotype.Component;

@Component
public class IndexCreator {
    private static final Logger logger = LoggerFactory.getLogger(IndexCreator.class);

    private final MongoOperations mongoOperations;

    public IndexCreator(MongoOperations mongoOperations) {
        this.mongoOperations = mongoOperations;
    }

    @EventListener(ContextRefreshedEvent.class)
    public void contextRefreshed() {
        ensureIndex(PatientReportingEvaluationStatus.class, new Index()
                .on("facilityId", Sort.Direction.ASC)
                .on("correlationId", Sort.Direction.ASC));
        ensureIndex(Resource.class, new Index()
                .on("facilityId", Sort.Direction.ASC)
                .on("correlationId", Sort.Direction.ASC)
                .on("resourceType", Sort.Direction.ASC)
                .on("resourceId", Sort.Direction.ASC));
    }

    private <T> void ensureIndex(Class<T> entityClass, Index index) {
        logger.info("Ensuring index on {}: {}", entityClass.getSimpleName(), index);
        try {
            mongoOperations.indexOps(entityClass).ensureIndex(index);
        } catch (Exception e) {
            logger.error("Failed to ensure index", e);
        }
    }
}
