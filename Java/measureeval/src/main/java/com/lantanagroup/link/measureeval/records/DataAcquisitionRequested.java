package com.lantanagroup.link.measureeval.records;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.lantanagroup.link.measureeval.models.QueryType;
import lombok.Getter;
import lombok.Setter;

import java.util.Date;
import java.util.List;

@Getter
@Setter
public class DataAcquisitionRequested {
    private String patientId;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<ResourceAcquired.ScheduledReport> scheduledReports;

    private QueryType queryType;

    @Getter
    @Setter
    public static class ScheduledReport {
        private String reportType;
        private Date startDate;
        private Date endDate;
    }
}
