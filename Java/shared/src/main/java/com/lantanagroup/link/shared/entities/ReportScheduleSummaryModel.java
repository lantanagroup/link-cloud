package com.lantanagroup.link.shared.entities;

import lombok.Getter;
import lombok.Setter;

import java.util.Date;
import java.util.List;

@Getter
@Setter
public class ReportScheduleSummaryModel {
    private String reportId;
    private String facilityId;
    private Date startDate;
    private Date endDate;
    private Date submitReportDateTime;
    private List<String> measures;
    private String payloadRootUri;
}
