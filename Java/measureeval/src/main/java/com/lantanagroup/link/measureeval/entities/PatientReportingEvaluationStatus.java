package com.lantanagroup.link.measureeval.entities;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.measureeval.models.NormalizationStatus;
import com.lantanagroup.link.measureeval.models.QueryType;
import com.lantanagroup.link.measureeval.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.ResourceType;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.Id;
import org.springframework.data.annotation.LastModifiedDate;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

@Getter
@Setter
public class PatientReportingEvaluationStatus {
    @Id
    private String id;

    private String facilityId;

    private String correlationId;
    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    private String reportableEvent;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<Report> reports = new ArrayList<>();

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<Resource> resources = new ArrayList<>();

    @CreatedDate
    private Date createdDate;

    @LastModifiedDate
    private Date modifiedDate;

    public boolean hasQueryType(QueryType queryType) {
        return resources.stream().anyMatch(resource -> resource.getQueryType() == queryType);
    }

    @Getter
    @Setter
    public static class Report {
        private String reportType;
        private Date startDate;
        private Date endDate;
        private Boolean reportable;
        private String frequency;
    }

    @Getter
    @Setter
    public static class Resource {
        private Boolean isPatientResource;
        private ResourceType resourceType;

        @JsonDeserialize(using = FhirIdDeserializer.class)
        private String resourceId;

        private QueryType queryType;
        private NormalizationStatus normalizationStatus;
    }
}
