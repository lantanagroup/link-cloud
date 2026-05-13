package com.lantanagroup.link.validation.entities;

import com.lantanagroup.link.shared.entities.ReportScheduleModel;
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

    public PreQualSummary(ReportScheduleModel reportSummary) {

        if(reportSummary == null) {
            throw new IllegalArgumentException("ReportSummary cannot be null");
        }

        this.facilityId = reportSummary.getFacilityId();
        this.report = new Report();
        this.report.setId(reportSummary.getId());
        this.report.setMeasures(reportSummary.getReportTypes());
        this.report.setPeriodStart(reportSummary.getReportStartDate() != null ? reportSummary.getReportStartDate().toString() : null);
        this.report.setPeriodEnd(reportSummary.getReportEndDate() != null ? reportSummary.getReportEndDate().toString() : null);
        this.report.setSubmittedTime(reportSummary.getSubmitReportDateTime());
    }
}

