package com.lantanagroup.link.measureeval.records;

import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.shared.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.util.Date;

@Getter
@Setter
public class ResourceEvaluated {
    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String measureReportId;

    private Boolean isReportable;

    private String reportType;

    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    private IBaseResource resource;

    private String reportTrackingId;

    @Getter
    @Setter
    public static class Key {
        private String facilityId;
        private String frequency;
        private Date startDate;
        private Date endDate;
    }
}
