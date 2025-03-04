package com.lantanagroup.link.validation.records;

import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class ValidationComplete {
    private String patientId;
    private boolean isValid;

    @Getter
    @Setter
    public static class Key {
        private String facilityId;
        private String reportId;
    }
}
