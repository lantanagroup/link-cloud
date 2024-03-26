package com.lantanagroup.link.measureeval.entities;

import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.Id;
import org.hl7.fhir.r4.model.ResourceType;

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

    private Types type;

    private String resourceJson;

    private Date timestamp;

    public enum Types {
        Original,
        Normalized,
        Evaluated
    }
}
