package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.Resource;
import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface ResourceRepository extends MongoRepository<Resource, String>, ResourceUpsertingRepository {
    List<Resource> findByFacilityIdAndCorrelationId(String facilityId, String correlationId);
}
