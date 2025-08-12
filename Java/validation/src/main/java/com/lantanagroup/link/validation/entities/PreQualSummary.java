package com.lantanagroup.link.validation.entities;

import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.Device;

import java.util.ArrayList;
import java.util.List;

@Getter
@Setter
public class PreQualSummary {
    private String facilityId;
    private Report report;
    private Device device;
    private Boolean prequalificationStatus;
    private List<Result> results = new ArrayList<>();
    private List<Category> categories = new ArrayList<>();

    public PreQualSummary(ReportScheduleSummaryModel reportSummary) {

        if(reportSummary == null) {
            throw new IllegalArgumentException("ReportSummary cannot be null");
        }

        this.facilityId = reportSummary.getFacilityId();
        this.report = new Report();
        this.report.setId(reportSummary.getReportId());
        this.report.setMeasures(reportSummary.getMeasures());
        this.report.setPeriodStart(reportSummary.getStartDate() != null ?
                reportSummary.getStartDate().toString() : null);
        this.report.setPeriodEnd(reportSummary.getEndDate() != null ?
                reportSummary.getEndDate().toString() : null);
        this.report.setSubmittedTime(reportSummary.getSubmitReportDateTime());
    }
}

