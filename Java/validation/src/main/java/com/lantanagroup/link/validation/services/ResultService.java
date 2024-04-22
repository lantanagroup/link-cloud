package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.repositories.ResultRepository;
import org.springframework.stereotype.Service;

@Service
public class ResultService {
    private final ResultRepository repository;

    public ResultService(ResultRepository repository) {
        this.repository = repository;
    }

    /*
    public List<ValidationResult> getValidationResults(String tenantId, String reportId) {
        return this.repository.findByTenantIdAndReportId(tenantId, reportId);
    }

     */
}
