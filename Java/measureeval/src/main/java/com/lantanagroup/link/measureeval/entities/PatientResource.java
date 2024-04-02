package com.lantanagroup.link.measureeval.entities;

import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.Id;
import org.hl7.fhir.r4.model.ResourceType;

import java.time.Instant;
import java.util.Date;

@Entity
public class PatientResource {
    @Id
    @GeneratedValue
    private String id;

    private String tenantId;

    private String patientId;

    private ResourceType resourceType;

    private String resourceId;

    private PatientResourceStages stage;

    private String resourceJson;

    private Instant timestamp;
}
