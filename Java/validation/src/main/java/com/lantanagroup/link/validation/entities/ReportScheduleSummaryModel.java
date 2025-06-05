package com.lantanagroup.link.validation.entities;

import lombok.Getter;
import lombok.Setter;

import java.util.Date;
import java.util.List;

@Getter
@Setter
public class ReportScheduleSummaryModel {
    public String reportId;
    public String facilityId;
    public Date startDate;
    public Date endDate;
    public Date submitReportDateTime;
    public List<String> measures;
}
