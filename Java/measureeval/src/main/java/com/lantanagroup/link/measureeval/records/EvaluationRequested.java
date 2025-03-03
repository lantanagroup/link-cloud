package com.lantanagroup.link.measureeval.records;

import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class EvaluationRequested {
    private String PreviousReportId;
    private String PatientId;
}
