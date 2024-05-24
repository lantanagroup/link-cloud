package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.ReportScheduledEntity;
import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;

@Repository
public interface ScheduledReportRepository
    extends MongoRepository<ReportScheduledEntity, String> {
    default Optional<ReportScheduledEntity> addOne(ReportScheduledEntity reportScheduledEntity) {
        return Optional.of(save(reportScheduledEntity));
    }
}
