package com.lantanagroup.link.measureeval.records;

import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.util.Date;

@Getter
@Setter
public class ResourceEvaluated {
    private String measureReportId;
    private String patientId;
    private IBaseResource resource;

    @Getter
    @Setter
    public static class Key {
        private String facilityId;
        private String reportType;
        private Date startDate;
        private Date endDate;
    }
}
