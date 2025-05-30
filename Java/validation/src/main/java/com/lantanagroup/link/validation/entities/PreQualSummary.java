package com.lantanagroup.link.validation.entities;

import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.Device;

import java.util.ArrayList;
import java.util.List;

@Getter
@Setter
public class PreQualSummary {
    public Report report;
    public Device device;
    public List<Result> results = new ArrayList<>();
    public List<Category> categories = new ArrayList<>();

    public PreQualSummary(ReportScheduleSummaryModel reportSummary) {
        this.report = new Report();
        this.report.setId(reportSummary.getReportId());
        this.report.setMeasures(reportSummary.getMeasures());
        this.report.setPeriodStart(reportSummary.getStartDate().toString());
        this.report.setPeriodEnd(reportSummary.getEndDate().toString());
        //this.report.setGeneratedTime(reportSummary.getGeneratedTime());
        this.report.setSubmittedTime(reportSummary.getSubmitReportDateTime());
    }
}

