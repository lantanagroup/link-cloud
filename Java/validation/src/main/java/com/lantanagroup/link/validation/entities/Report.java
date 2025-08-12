package com.lantanagroup.link.validation.entities;

import lombok.Getter;
import lombok.Setter;
import org.hl7.fhir.r4.model.Device;

import java.util.Date;
import java.util.List;

@Getter
@Setter
public class Report {
    private String id;
    private List<String> measures;
    private String periodStart;
    private String periodEnd;
    private Device deviceInfo;
    private String queryPlan;
    private Date generatedTime;
    private Date submittedTime;
}

