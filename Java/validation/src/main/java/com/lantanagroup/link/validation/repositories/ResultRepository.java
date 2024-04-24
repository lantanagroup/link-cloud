package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.ResultEntity;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface ResultRepository extends JpaRepository<ResultEntity, Long> {
    void deleteByTenantId(String tenantId);

    void deleteByTenantIdAndReportId(String tenantId, String reportId);
}
