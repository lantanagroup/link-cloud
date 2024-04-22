package com.lantanagroup.link.validation.entities;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.Id;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.OperationOutcome;

@Entity
@Getter
@Setter
public class Result {
    @Id
    @GeneratedValue
    private Long id;

    @Column(nullable = false)
    private String tenantId;

    @Column(nullable = false)
    private String reportId;

    @Column(nullable = false, columnDefinition = "varchar(max)")
    private String message;

    @Column(nullable = false, length = 4096)
    private String expression;

    @Column(nullable = false)
    private OperationOutcome.IssueSeverity severity;
}
