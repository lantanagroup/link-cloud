package com.lantanagroup.link.measureeval.entities;

import com.lantanagroup.link.measureeval.models.NormalizationStatus;
import com.lantanagroup.link.measureeval.models.QueryType;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.ResourceType;

import java.time.Instant;
import java.util.List;

@Getter
@Setter
public class PatientReportingEvaluationStatus {
    private String id;
    private String facilityId;
    private String patientId;
    private String correlationId;
    private List<Report> reports;
    private List<Resource> resources;
    private Instant createdDate;
    private Instant modifiedDate;

    @Getter
    @Setter
    public static class Report {
        private String reportId;
        private Boolean reportable;
    }

    @Getter
    @Setter
    public static class Resource {
        private ResourceType resourceType;
        private String resourceId;
        private NormalizationStatus normalizationStatus;
        private QueryType queryType;
        private boolean isPatientResource;
    }
}
