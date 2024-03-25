package com.lantanagroup.link.validation.model;

import lombok.Getter;
import lombok.Setter;

import java.util.Date;

@Getter
@Setter
public class PatientEvaluatedModel {
    private String tenantId;
    private String patientId;
    private Date timestamp;
}
