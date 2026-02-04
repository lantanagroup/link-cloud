package com.lantanagroup.link.shared.entities;

import lombok.Getter;
import lombok.Setter;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

@Getter
@Setter
public class ReportScheduleModel {
    private String id;
    private Date createDate;
    private Date modifyDate;
    private String facilityId;
    private Date reportStartDate;
    private Date reportEndDate;
    private Date submitReportDateTime;
    private boolean enableSubmission = true;
    private boolean endOfReportPeriodJobHasRun = false;
    private List<String> reportTypes = new ArrayList<>();
    private AdHocType adHocType;
    private Frequency frequency;
    private String payloadRootUri;
    private ScheduleStatus status = ScheduleStatus.New;

}
