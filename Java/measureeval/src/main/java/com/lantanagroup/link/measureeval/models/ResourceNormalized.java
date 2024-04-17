package com.lantanagroup.link.measureeval.models;

import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.instance.model.api.IBaseResource;

import java.util.Date;
import java.util.List;

@Getter
@Setter
public class ResourceNormalized {
    private String patientId;
    private QueryType queryType;
    private IBaseResource resource;
    private List<ScheduledReport> scheduledReports;

    @Getter
    @Setter
    public static class ScheduledReport {
        private String reportType;
        private Date startDate;
        private Date endDate;
    }
}
