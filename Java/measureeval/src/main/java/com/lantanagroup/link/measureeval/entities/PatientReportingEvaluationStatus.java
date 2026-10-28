package com.lantanagroup.link.measureeval.entities;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.shared.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;
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

    @CreatedDate
    private Date createdDate;

    @LastModifiedDate
    private Date modifiedDate;

    @Getter
    @Setter
    public static class Report {
        private String reportType;
        private Date startDate;
        private Date endDate;
        private Boolean reportable;
        private String frequency;
        private String reportTrackingId;
    }
}
