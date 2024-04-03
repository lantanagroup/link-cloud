package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.repositories.ValidationResultRepository;
import org.springframework.stereotype.Service;

@Service
public class ValidationResultService {
    private final ValidationResultRepository repository;

    public ValidationResultService(ValidationResultRepository repository) {
        this.repository = repository;
    }

    /*
    public List<ValidationResult> getValidationResults(String tenantId, String reportId) {
        return this.repository.findByTenantIdAndReportId(tenantId, reportId);
    }

     */
}
