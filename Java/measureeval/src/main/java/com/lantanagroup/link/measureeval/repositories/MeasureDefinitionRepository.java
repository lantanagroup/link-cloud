package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.MeasureDefinition;
import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface MeasureDefinitionRepository extends MongoRepository<MeasureDefinition, String> {
}
