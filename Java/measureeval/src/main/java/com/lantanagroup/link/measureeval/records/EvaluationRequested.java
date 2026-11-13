package com.lantanagroup.link.measureeval.records;

import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class EvaluationRequested {
    private String PreviousReportId;
    private String FacilityId;
    private String PatientId;
    private String ReportTrackingId;
}
