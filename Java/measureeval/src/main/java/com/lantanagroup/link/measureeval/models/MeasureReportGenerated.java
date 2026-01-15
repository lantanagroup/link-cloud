package com.lantanagroup.link.measureeval.models;

import lombok.Data;

import java.util.Date;

public class MeasureReportGenerated {
    @Data
    public static class Key {
        private String facilityId;
        private Date startDate;
        private Date endDate;
        private String frequency;
    }

    @Data
    public static class Value {
        private String measureReportId;
        private String patientId;
        private String reportType;
        private Boolean isReportable;
        private String reportTrackingId;
        private String measureReportURI;
        private String measureReportFileName;
    }
}
