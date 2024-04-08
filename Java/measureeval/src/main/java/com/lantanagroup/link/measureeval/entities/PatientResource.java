package com.lantanagroup.link.measureeval.entities;

import org.hl7.fhir.r4.model.ResourceType;
import org.springframework.data.annotation.Id;

import java.time.Instant;

public class PatientResource {
    @Id
    private String id;

    private String tenantId;

    private String patientId;

    private ResourceType resourceType;

    private String resourceId;

    private PatientResourceStages stage;

    private String resourceJson;

    private Instant timestamp;
}
