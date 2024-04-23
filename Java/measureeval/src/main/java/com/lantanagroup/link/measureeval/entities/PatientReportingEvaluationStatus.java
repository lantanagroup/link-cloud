package com.lantanagroup.link.measureeval.entities;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.lantanagroup.link.measureeval.models.NormalizationStatus;
import com.lantanagroup.link.measureeval.models.QueryType;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.ResourceType;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.Id;
import org.springframework.data.annotation.LastModifiedDate;

import java.util.Date;
import java.util.List;

@Getter
@Setter
public class PatientReportingEvaluationStatus {
    @Id
    private String id;
    private String facilityId;
    private String patientId;
    private String correlationId;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<Report> reports;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<Resource> resources;

    @CreatedDate
    private Date createdDate;

    @LastModifiedDate
    private Date modifiedDate;

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
