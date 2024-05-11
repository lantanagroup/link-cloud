package com.lantanagroup.link.measureeval.records;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.fasterxml.jackson.databind.annotation.JsonDeserialize;
import com.lantanagroup.link.measureeval.models.QueryType;
import com.lantanagroup.link.measureeval.serdes.FhirIdDeserializer;
import lombok.Getter;
import lombok.Setter;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

@Getter
@Setter
public class DataAcquisitionRequested {
    @JsonDeserialize(using = FhirIdDeserializer.class)
    private String patientId;

    private QueryType queryType;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<ScheduledReport> scheduledReports = new ArrayList<>();

    @Getter
    @Setter
    public static class ScheduledReport {
        private String reportType;
        private Date startDate;
        private Date endDate;
    }
}
