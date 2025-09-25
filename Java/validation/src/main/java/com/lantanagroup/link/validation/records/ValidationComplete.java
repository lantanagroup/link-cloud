package com.lantanagroup.link.validation.records;

import com.fasterxml.jackson.annotation.JsonProperty;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class ValidationComplete {
    private String patientId;
    private String reportTrackingId;

    @JsonProperty("isValid")
    private boolean valid;
}
