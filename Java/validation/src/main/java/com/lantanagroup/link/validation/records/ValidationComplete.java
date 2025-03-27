package com.lantanagroup.link.validation.records;

import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class ValidationComplete {
    private String patientId;
    private String reportTrackingId;
    private boolean isValid;
}
