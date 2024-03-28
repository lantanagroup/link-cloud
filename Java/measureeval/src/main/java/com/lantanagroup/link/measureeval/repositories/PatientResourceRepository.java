package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.PatientResource;
import org.springframework.data.mongodb.repository.MongoRepository;

public interface PatientResourceRepository extends MongoRepository<PatientResource, String> {
}
