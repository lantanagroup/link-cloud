package com.lantanagroup.link.measureeval.records;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.lantanagroup.link.measureeval.models.QueryType;
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

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<ScheduledReport> scheduledReports;

    @Getter
    @Setter
    public static class ScheduledReport {
        private String reportType;
        private Date startDate;
        private Date endDate;
    }
}
