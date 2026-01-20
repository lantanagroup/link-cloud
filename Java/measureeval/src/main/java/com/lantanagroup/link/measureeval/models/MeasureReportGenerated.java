package com.lantanagroup.link.measureeval.models;

import lombok.Data;

public class MeasureReportGenerated {
    @Data
    public static class Value {
        private String measureReportId;
        private String facilityId;
        private String patientId;
        private String reportType;
        private Boolean isReportable;
        private String reportTrackingId;
        private String measureReportURI;
        private String measureReportFileName;
    }
}
