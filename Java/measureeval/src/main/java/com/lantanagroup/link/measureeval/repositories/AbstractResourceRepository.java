package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.configs.LinkConfig;
import com.lantanagroup.link.measureeval.entities.AbstractResourceEntity;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.measureeval.entities.PatientResource;
import com.lantanagroup.link.measureeval.entities.SharedResource;
import com.lantanagroup.link.measureeval.records.AbstractResourceRecord;
import org.hl7.fhir.r4.model.ResourceType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.mongodb.core.MongoOperations;
import org.springframework.data.mongodb.core.query.Criteria;
import org.springframework.data.mongodb.core.query.Query;
import org.springframework.stereotype.Repository;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.concurrent.*;
import java.util.stream.Collectors;

import static org.springframework.data.mongodb.core.query.Criteria.byExample;

@Repository
public class AbstractResourceRepository {
    private static final Logger logger = LoggerFactory.getLogger(AbstractResourceRepository.class);
    private final MongoOperations mongoOperations;
    private final LinkConfig linkConfig;

    public AbstractResourceRepository(MongoOperations mongoOperations, LinkConfig linkConfig) {
        this.mongoOperations = mongoOperations;
        this.linkConfig = linkConfig;
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

    public List<AbstractResourceEntity> findResources(String facilityId, boolean isShared, List<PatientReportingEvaluationStatus.Resource> resourceReferences) {
        if (resourceReferences == null || resourceReferences.isEmpty()) {
            return new ArrayList<>();
        }

        List<CompletableFuture<List<AbstractResourceEntity>>> futures = new ArrayList<>();
        boolean useConfiguredThreadCount = linkConfig.getMaxCollectResourcesThreads() != null &&
                linkConfig.getMaxCollectResourcesThreads() > 0 &&
                linkConfig.getMaxCollectResourcesThreads() < Runtime.getRuntime().availableProcessors();
        int threadCount = Runtime.getRuntime().availableProcessors();

        if (useConfiguredThreadCount) {
            threadCount = linkConfig.getMaxCollectResourcesThreads();
            logger.debug("Using configured thread count of {}", linkConfig.getMaxCollectResourcesThreads());
        } else {
            logger.debug("Using maximum thread count of {} for available processors", threadCount);
        }

        ExecutorService executor = Executors.newFixedThreadPool(threadCount);

        try {
            // Batch the searches so that it doesn't exceed mongo's query limitations
            List<PatientReportingEvaluationStatus.Resource> remainingResources = new ArrayList<>(resourceReferences);
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
                    List<Criteria> finalBatchCriteria = batchCriteria;
                    CompletableFuture<List<AbstractResourceEntity>> future = CompletableFuture.supplyAsync(() -> {
                        Criteria combinedCriteria = new Criteria().orOperator(finalBatchCriteria.toArray(new Criteria[0]));
                        Query query = new Query(combinedCriteria);
                        List<? extends AbstractResourceEntity> results = isShared ?
                                mongoOperations.find(query, SharedResource.class) :
                                mongoOperations.find(query, PatientResource.class);
                        return new ArrayList<>(results);
                    }, executor);
                    futures.add(future);
                }
            }

            return futures.stream()
                    .map(CompletableFuture::join)
                    .flatMap(List::stream)
                    .collect(Collectors.toList());
        } finally {
            executor.shutdown();
        }
    }
}
